# EtlCargaDimensiones (.NET 8 - C#)

Solución ETL desarrollada en **.NET 8 (C#)** bajo **Clean Architecture** para la carga de las 6 tablas de dimensiones del Data Warehouse **`DW_AnalisisVentas`** desde la base de datos operacional **`SistemaAnalisisVentas`** en **Microsoft SQL Server**.

## Dimensiones Cargadas
1. **`dbo.DimFuente`** — Upsert idempotente desde `FuenteDatos`.
2. **`dbo.DimUbicacion`** — Upsert idempotente desde `Sucursal` + `Ciudad` + `Provincia`.
3. **`dbo.DimVendedor`** — Upsert idempotente desde `Empleado` + `Sucursal`.
4. **`dbo.DimCliente`** — **SCD Tipo 2** (`FechaInicio`, `FechaFin`, `EsActual`) desde `Cliente` + `Ciudad`.
5. **`dbo.DimProducto`** — **SCD Tipo 2** (`FechaInicio`, `FechaFin`, `EsActual`) desde `Producto` + `Categoria` + `Proveedor`.
6. **`dbo.DimFecha`** — Calendario sin duplicados (`FechaKey` `YYYYMMDD`, nombres de día/mes en español, trimestre, semana ISO y fin de semana) a partir de las fechas operacionales.

## Estructura del Proyecto
- `src/EtlDimensiones.Domain`: Entidades de dimensiones, resultados de carga e interfaces (`IDimensionLoader`).
- `src/EtlDimensiones.Application`: Orquestador de carga (`DimensionLoadOrchestrator`) y opciones de configuración.
- `src/EtlDimensiones.Infrastructure`: Cargadores concretos con `Microsoft.Data.SqlClient` (ADO.NET asíncrono y transacciones seguras).
- `src/EtlDimensiones.Console`: Punto de entrada (`Program.cs`) y configuración (`appsettings.json`).

## Ejecución Local
```bash
dotnet restore
dotnet build
dotnet run --project src/EtlDimensiones.Console
```
