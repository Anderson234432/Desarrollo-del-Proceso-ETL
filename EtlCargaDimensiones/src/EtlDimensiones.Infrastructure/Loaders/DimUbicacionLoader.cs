using System.Diagnostics;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimUbicacionLoader : IDimensionLoader
{
    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimUbicacion";

    public DimUbicacionLoader(IConfiguration configuration)
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
            var rows = new List<(string Pais, string Region, string Ciudad, string Sucursal)>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = @"
                    SELECT p.Region, c.Nombre AS Ciudad, s.Nombre AS Sucursal
                    FROM dbo.Sucursal s
                    INNER JOIN dbo.Ciudad c ON s.CiudadID = c.CiudadID
                    INNER JOIN dbo.Provincia p ON c.ProvinciaID = p.ProvinciaID";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add((
                        "República Dominicana",
                        SqlHelper.Truncate(reader.GetString(0), 60),
                        SqlHelper.Truncate(reader.GetString(1), 80),
                        SqlHelper.Truncate(reader.GetString(2), 80)));
                }
            }

            extracted = rows.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var r in rows)
                {
                    const string checkSql = @"
                        SELECT UbicacionKey, Region
                        FROM dbo.DimUbicacion
                        WHERE Pais = @Pais AND Ciudad = @Ciudad AND Sucursal = @Sucursal";
                    int? existingKey = null;
                    string? existingRegion = null;

                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Pais", r.Pais);
                        checkCmd.Parameters.AddWithValue("@Ciudad", r.Ciudad);
                        checkCmd.Parameters.AddWithValue("@Sucursal", r.Sucursal);
                        await using var rd = await checkCmd.ExecuteReaderAsync(cancellationToken);
                        if (await rd.ReadAsync(cancellationToken))
                        {
                            existingKey = rd.GetInt32(0);
                            existingRegion = rd.GetString(1);
                        }
                    }

                    if (existingKey is null)
                    {
                        const string insSql = @"
                            INSERT INTO dbo.DimUbicacion (Pais, Region, Ciudad, Sucursal)
                            VALUES (@Pais, @Region, @Ciudad, @Sucursal)";
                        await using var ins = new SqlCommand(insSql, dw);
                        ins.Parameters.AddWithValue("@Pais", r.Pais);
                        ins.Parameters.AddWithValue("@Region", r.Region);
                        ins.Parameters.AddWithValue("@Ciudad", r.Ciudad);
                        ins.Parameters.AddWithValue("@Sucursal", r.Sucursal);
                        await ins.ExecuteNonQueryAsync(cancellationToken);
                        inserted++;
                    }
                    else if (!string.Equals(existingRegion, r.Region, StringComparison.Ordinal))
                    {
                        const string updSql = "UPDATE dbo.DimUbicacion SET Region = @Region WHERE UbicacionKey = @Key";
                        await using var upd = new SqlCommand(updSql, dw);
                        upd.Parameters.AddWithValue("@Region", r.Region);
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
