# EtlOpinionesWorker (.NET 8)

Worker Service desarrollado en **.NET 8** bajo principios **Clean Architecture** y **SOLID** para el proceso de Extracción (**E** de ETL) desde tres fuentes heterogéneas:

1. **CSV (`CsvExtractor`)**: Lectura asíncrona de encuestas internas mediante `CsvHelper`.
2. **Base de Datos Relacional (`DatabaseExtractor`)**: Consulta de reseñas mediante `Microsoft.Data.SqlClient` (ADO.NET asíncrono).
3. **API REST (`ApiExtractor`)**: Consumo de comentarios externos mediante `IHttpClientFactory`.

## Estructura de la Solución

- `src/EtlOpiniones.Domain`: Entidades (`StagingRecord`, `EncuestaInterna`, `ResenaDb`, `ComentarioApi`) e interfaces (`IExtractor`, `IStagingWriter`, `IDataLoader`).
- `src/EtlOpiniones.Application`: Opciones tipadas (`EtlSettings`), DTOs (`ExtractionResult`) y orquestador paralelo (`ExtractionOrchestrator`).
- `src/EtlOpiniones.Infrastructure`: Implementaciones concretas de extractores, persistencia en staging JSON (`StagingWriter`), `DataLoader` y `LoggerService`.
- `src/EtlOpiniones.Worker`: Punto de entrada `Program.cs`, `Worker.cs` (`BackgroundService`) y `appsettings.json`.

## Ejecución Local y Seguridad

```bash
dotnet restore
dotnet build
# Configurar secretos localmente sin exponer credenciales en Git:
dotnet user-secrets --project src/EtlOpiniones.Worker set "Secrets:ComentariosApiKey" "TU_API_KEY"
dotnet run --project src/EtlOpiniones.Worker
```
