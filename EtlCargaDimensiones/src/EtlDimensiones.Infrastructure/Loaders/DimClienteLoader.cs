using System.Diagnostics;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimClienteLoader : IDimensionLoader
{
    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimCliente";

    public DimClienteLoader(IConfiguration configuration)
    {
        _sourceConn = SqlHelper.GetRequiredConnection(configuration, "SourceDb");
        _dwConn = SqlHelper.GetRequiredConnection(configuration, "DataWarehouseDb");
    }

    public async Task<DimensionLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int extracted = 0, inserted = 0, updated = 0, skipped = 0;

        try
        {
            var rows = new List<(string IdOrigen, string Nombre, string Tipo, string Segmento, string? Email, string Pais, string? Ciudad, DateTime FechaRegistro)>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = @"
                    SELECT CAST(c.ClienteID AS VARCHAR(30)) AS IdOrigen,
                           c.Nombre,
                           c.TipoCliente,
                           c.Email,
                           ci.Nombre AS Ciudad,
                           c.FechaRegistro
                    FROM dbo.Cliente c
                    LEFT JOIN dbo.Ciudad ci ON c.CiudadID = ci.CiudadID";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var tipo = SqlHelper.Truncate(reader.GetString(2), 10);
                    var segmento = tipo.Contains("Emp", StringComparison.OrdinalIgnoreCase) ||
                                   tipo.Contains("Corp", StringComparison.OrdinalIgnoreCase) ||
                                   tipo.Contains("B2B", StringComparison.OrdinalIgnoreCase)
                        ? "Corporativo"
                        : "Consumo General";

                    rows.Add((
                        SqlHelper.Truncate(reader.GetString(0), 30),
                        SqlHelper.Truncate(reader.GetString(1), 120),
                        tipo,
                        segmento,
                        reader.IsDBNull(3) ? null : SqlHelper.Truncate(reader.GetString(3), 100),
                        "República Dominicana",
                        reader.IsDBNull(4) ? null : SqlHelper.Truncate(reader.GetString(4), 80),
                        reader.GetDateTime(5).Date));
                }
            }

            extracted = rows.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var r in rows)
                {
                    // SCD Tipo 2: buscamos el registro activo actual
                    const string checkSql = @"
                        SELECT ClienteKey, Nombre, TipoCliente, Email, Ciudad
                        FROM dbo.DimCliente
                        WHERE ClienteID_Origen = @Id AND EsActual = 1";
                    int? existingKey = null;
                    string? existingNombre = null, existingTipo = null, existingEmail = null, existingCiudad = null;

                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", r.IdOrigen);
                        await using var rd = await checkCmd.ExecuteReaderAsync(cancellationToken);
                        if (await rd.ReadAsync(cancellationToken))
                        {
                            existingKey = rd.GetInt32(0);
                            existingNombre = rd.GetString(1);
                            existingTipo = rd.GetString(2);
                            existingEmail = rd.IsDBNull(3) ? null : rd.GetString(3);
                            existingCiudad = rd.IsDBNull(4) ? null : rd.GetString(4);
                        }
                    }

                    if (existingKey is null)
                    {
                        await InsertClienteAsync(dw, r, r.FechaRegistro, cancellationToken);
                        inserted++;
                    }
                    else if (!string.Equals(existingNombre, r.Nombre, StringComparison.Ordinal) ||
                             !string.Equals(existingTipo, r.Tipo, StringComparison.Ordinal) ||
                             !string.Equals(existingEmail, r.Email, StringComparison.Ordinal) ||
                             !string.Equals(existingCiudad, r.Ciudad, StringComparison.Ordinal))
                    {
                        // Cerramos versión anterior e insertamos nueva versión vigente
                        const string closeSql = @"
                            UPDATE dbo.DimCliente
                            SET FechaFin = CAST(GETDATE() AS DATE), EsActual = 0
                            WHERE ClienteKey = @Key";
                        await using var closeCmd = new SqlCommand(closeSql, dw);
                        closeCmd.Parameters.AddWithValue("@Key", existingKey.Value);
                        await closeCmd.ExecuteNonQueryAsync(cancellationToken);

                        await InsertClienteAsync(dw, r, DateTime.Today, cancellationToken);
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            sw.Stop();
            return new DimensionLoadResult(DimensionName, extracted, inserted, updated, skipped, sw.ElapsedMilliseconds, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DimensionLoadResult(DimensionName, extracted, inserted, updated, skipped, sw.ElapsedMilliseconds, false, ex.Message);
        }
    }

    private static async Task InsertClienteAsync(
        SqlConnection dw,
        (string IdOrigen, string Nombre, string Tipo, string Segmento, string? Email, string Pais, string? Ciudad, DateTime FechaRegistro) r,
        DateTime fechaInicio,
        CancellationToken ct)
    {
        const string insSql = @"
            INSERT INTO dbo.DimCliente
                (ClienteID_Origen, Nombre, TipoCliente, Segmento, Email, Pais, Ciudad, FechaInicio, FechaFin, EsActual)
            VALUES
                (@Id, @Nombre, @Tipo, @Segmento, @Email, @Pais, @Ciudad, @FechaInicio, NULL, 1)";
        await using var ins = new SqlCommand(insSql, dw);
        ins.Parameters.AddWithValue("@Id", r.IdOrigen);
        ins.Parameters.AddWithValue("@Nombre", r.Nombre);
        ins.Parameters.AddWithValue("@Tipo", r.Tipo);
        ins.Parameters.AddWithValue("@Segmento", r.Segmento);
        ins.Parameters.AddWithValue("@Email", SqlHelper.ToDbNullable(r.Email, 100));
        ins.Parameters.AddWithValue("@Pais", r.Pais);
        ins.Parameters.AddWithValue("@Ciudad", SqlHelper.ToDbNullable(r.Ciudad, 80));
        ins.Parameters.AddWithValue("@FechaInicio", fechaInicio);
        await ins.ExecuteNonQueryAsync(ct);
    }
}
