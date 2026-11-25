using System.Diagnostics;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimProductoLoader : IDimensionLoader
{
    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimProducto";

    public DimProductoLoader(IConfiguration configuration)
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
            var rows = new List<(string IdOrigen, string? Sku, string Nombre, string Categoria, string? Marca, decimal PrecioLista)>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = @"
                    SELECT CAST(p.ProductoID AS VARCHAR(30)) AS IdOrigen,
                           p.CodigoSKU,
                           p.Nombre,
                           cat.Nombre AS Categoria,
                           pr.Nombre AS Marca,
                           p.PrecioVenta
                    FROM dbo.Producto p
                    INNER JOIN dbo.Categoria cat ON p.CategoriaID = cat.CategoriaID
                    LEFT JOIN dbo.Proveedor pr ON p.ProveedorID = pr.ProveedorID";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add((
                        SqlHelper.Truncate(reader.GetString(0), 30),
                        reader.IsDBNull(1) ? null : SqlHelper.Truncate(reader.GetString(1), 30),
                        SqlHelper.Truncate(reader.GetString(2), 120),
                        SqlHelper.Truncate(reader.GetString(3), 60),
                        reader.IsDBNull(4) ? null : SqlHelper.Truncate(reader.GetString(4), 60),
                        reader.GetDecimal(5)));
                }
            }

            extracted = rows.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var r in rows)
                {
                    // SCD Tipo 2: buscamos versión vigente
                    const string checkSql = @"
                        SELECT ProductoKey, Nombre, Categoria, Marca, PrecioLista
                        FROM dbo.DimProducto
                        WHERE ProductoID_Origen = @Id AND EsActual = 1";
                    int? existingKey = null;
                    string? existingNombre = null, existingCat = null, existingMarca = null;
                    decimal? existingPrecio = null;

                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", r.IdOrigen);
                        await using var rd = await checkCmd.ExecuteReaderAsync(cancellationToken);
                        if (await rd.ReadAsync(cancellationToken))
                        {
                            existingKey = rd.GetInt32(0);
                            existingNombre = rd.GetString(1);
                            existingCat = rd.GetString(2);
                            existingMarca = rd.IsDBNull(3) ? null : rd.GetString(3);
                            existingPrecio = rd.IsDBNull(4) ? null : rd.GetDecimal(4);
                        }
                    }

                    if (existingKey is null)
                    {
                        await InsertProductoAsync(dw, r, cancellationToken);
                        inserted++;
                    }
                    else if (!string.Equals(existingNombre, r.Nombre, StringComparison.Ordinal) ||
                             !string.Equals(existingCat, r.Categoria, StringComparison.Ordinal) ||
                             !string.Equals(existingMarca, r.Marca, StringComparison.Ordinal) ||
                             existingPrecio != r.PrecioLista)
                    {
                        const string closeSql = @"
                            UPDATE dbo.DimProducto
                            SET FechaFin = CAST(GETDATE() AS DATE), EsActual = 0
                            WHERE ProductoKey = @Key";
                        await using var closeCmd = new SqlCommand(closeSql, dw);
                        closeCmd.Parameters.AddWithValue("@Key", existingKey.Value);
                        await closeCmd.ExecuteNonQueryAsync(cancellationToken);

                        await InsertProductoAsync(dw, r, cancellationToken);
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

    private static async Task InsertProductoAsync(
        SqlConnection dw,
        (string IdOrigen, string? Sku, string Nombre, string Categoria, string? Marca, decimal PrecioLista) r,
        CancellationToken ct)
    {
        const string insSql = @"
            INSERT INTO dbo.DimProducto
                (ProductoID_Origen, CodigoSKU, Nombre, Categoria, Marca, PrecioLista, FechaInicio, FechaFin, EsActual)
            VALUES
                (@Id, @Sku, @Nombre, @Categoria, @Marca, @Precio, CAST(GETDATE() AS DATE), NULL, 1)";
        await using var ins = new SqlCommand(insSql, dw);
        ins.Parameters.AddWithValue("@Id", r.IdOrigen);
        ins.Parameters.AddWithValue("@Sku", SqlHelper.ToDbNullable(r.Sku, 30));
        ins.Parameters.AddWithValue("@Nombre", r.Nombre);
        ins.Parameters.AddWithValue("@Categoria", r.Categoria);
        ins.Parameters.AddWithValue("@Marca", SqlHelper.ToDbNullable(r.Marca, 60));
        ins.Parameters.AddWithValue("@Precio", r.PrecioLista);
        await ins.ExecuteNonQueryAsync(ct);
    }
}
