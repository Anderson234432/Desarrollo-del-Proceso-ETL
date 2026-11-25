using System.Diagnostics;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimFuenteLoader : IDimensionLoader
{
    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimFuente";

    public DimFuenteLoader(IConfiguration configuration)
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
            var rows = new List<(string Nombre, string Tipo, string? Descripcion)>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = "SELECT Nombre, TipoFuente, Descripcion FROM dbo.FuenteDatos";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add((
                        SqlHelper.Truncate(reader.GetString(0), 60),
                        NormalizeTipoFuente(reader.GetString(1)),
                        reader.IsDBNull(2) ? null : reader.GetString(2)));
                }
            }

            extracted = rows.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var r in rows)
                {
                    const string checkSql = "SELECT FuenteKey, TipoFuente, Descripcion FROM dbo.DimFuente WHERE NombreFuente = @Nombre";
                    int? existingKey = null;
                    string? existingTipo = null;
                    string? existingDesc = null;

                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Nombre", r.Nombre);
                        await using var rd = await checkCmd.ExecuteReaderAsync(cancellationToken);
                        if (await rd.ReadAsync(cancellationToken))
                        {
                            existingKey = rd.GetInt32(0);
                            existingTipo = rd.GetString(1);
                            existingDesc = rd.IsDBNull(2) ? null : rd.GetString(2);
                        }
                    }

                    var newDesc = r.Descripcion is null ? null : SqlHelper.Truncate(r.Descripcion, 200);
                    if (existingKey is null)
                    {
                        const string insSql = @"
                            INSERT INTO dbo.DimFuente (NombreFuente, TipoFuente, Descripcion)
                            VALUES (@Nombre, @Tipo, @Desc)";
                        await using var ins = new SqlCommand(insSql, dw);
                        ins.Parameters.AddWithValue("@Nombre", r.Nombre);
                        ins.Parameters.AddWithValue("@Tipo", r.Tipo);
                        ins.Parameters.AddWithValue("@Desc", SqlHelper.ToDbNullable(newDesc, 200));
                        await ins.ExecuteNonQueryAsync(cancellationToken);
                        inserted++;
                    }
                    else if (!string.Equals(existingTipo, r.Tipo, StringComparison.Ordinal) ||
                             !string.Equals(existingDesc, newDesc, StringComparison.Ordinal))
                    {
                        const string updSql = @"
                            UPDATE dbo.DimFuente
                            SET TipoFuente = @Tipo, Descripcion = @Desc
                            WHERE FuenteKey = @Key";
                        await using var upd = new SqlCommand(updSql, dw);
                        upd.Parameters.AddWithValue("@Tipo", r.Tipo);
                        upd.Parameters.AddWithValue("@Desc", SqlHelper.ToDbNullable(newDesc, 200));
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

    private static string NormalizeTipoFuente(string rawTipo)
    {
        var upper = rawTipo.Trim().ToUpperInvariant();
        return upper switch
        {
            "ARCHIVO_CSV" or "CSV" or "EXCEL" => "CSV",
            "ECOMMERCE" or "API" or "API_REST" => "API_REST",
            _ => "BD_EXTERNA"
        };
    }
}
