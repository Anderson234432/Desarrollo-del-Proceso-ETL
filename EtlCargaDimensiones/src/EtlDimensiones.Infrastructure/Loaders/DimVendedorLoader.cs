using System.Diagnostics;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimVendedorLoader : IDimensionLoader
{
    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimVendedor";

    public DimVendedorLoader(IConfiguration configuration)
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
            var rows = new List<(string IdOrigen, string Nombre, string? Equipo)>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = @"
                    SELECT CAST(e.EmpleadoID AS VARCHAR(30)) AS IdOrigen,
                           LTRIM(RTRIM(e.Nombre + ' ' + e.Apellido)) AS NombreCompleto,
                           e.Cargo
                    FROM dbo.Empleado e";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add((
                        SqlHelper.Truncate(reader.GetString(0), 30),
                        SqlHelper.Truncate(reader.GetString(1), 120),
                        reader.IsDBNull(2) ? null : SqlHelper.Truncate(reader.GetString(2), 60)));
                }
            }

            extracted = rows.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var r in rows)
                {
                    const string checkSql = "SELECT VendedorKey, Nombre, Equipo FROM dbo.DimVendedor WHERE VendedorID_Origen = @Id";
                    int? existingKey = null;
                    string? existingNombre = null;
                    string? existingEquipo = null;

                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", r.IdOrigen);
                        await using var rd = await checkCmd.ExecuteReaderAsync(cancellationToken);
                        if (await rd.ReadAsync(cancellationToken))
                        {
                            existingKey = rd.GetInt32(0);
                            existingNombre = rd.GetString(1);
                            existingEquipo = rd.IsDBNull(2) ? null : rd.GetString(2);
                        }
                    }

                    if (existingKey is null)
                    {
                        const string insSql = @"
                            INSERT INTO dbo.DimVendedor (VendedorID_Origen, Nombre, Equipo)
                            VALUES (@Id, @Nombre, @Equipo)";
                        await using var ins = new SqlCommand(insSql, dw);
                        ins.Parameters.AddWithValue("@Id", r.IdOrigen);
                        ins.Parameters.AddWithValue("@Nombre", r.Nombre);
                        ins.Parameters.AddWithValue("@Equipo", SqlHelper.ToDbNullable(r.Equipo, 60));
                        await ins.ExecuteNonQueryAsync(cancellationToken);
                        inserted++;
                    }
                    else if (!string.Equals(existingNombre, r.Nombre, StringComparison.Ordinal) ||
                             !string.Equals(existingEquipo, r.Equipo, StringComparison.Ordinal))
                    {
                        const string updSql = @"
                            UPDATE dbo.DimVendedor
                            SET Nombre = @Nombre, Equipo = @Equipo
                            WHERE VendedorKey = @Key";
                        await using var upd = new SqlCommand(updSql, dw);
                        upd.Parameters.AddWithValue("@Nombre", r.Nombre);
                        upd.Parameters.AddWithValue("@Equipo", SqlHelper.ToDbNullable(r.Equipo, 60));
                        upd.Parameters.AddWithValue("@Key", existingKey.Value);
                        await upd.ExecuteNonQueryAsync(cancellationToken);
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
}
