using Npgsql;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(
    Environment.GetEnvironmentVariable("DB_CONNECTION") ?? ""));

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/tareas", async (NpgsqlDataSource db) =>
{
    var tareas = new List<object>();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, titulo, descripcion, completada, created_at FROM tareas ORDER BY created_at DESC";
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        tareas.Add(new { id = reader.GetGuid(0), titulo = reader.GetString(1), descripcion = reader.IsDBNull(2) ? null : reader.GetString(2), completada = reader.GetBoolean(3), createdAt = reader.GetDateTime(4) });
    return Results.Ok(tareas);
});

app.MapGet("/tareas/{id}", async (Guid id, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, titulo, descripcion, completada, created_at FROM tareas WHERE id = @id";
    cmd.Parameters.AddWithValue("id", id);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.NotFound(new { error = "Tarea no encontrada" });
    return Results.Ok(new { id = reader.GetGuid(0), titulo = reader.GetString(1), descripcion = reader.IsDBNull(2) ? null : reader.GetString(2), completada = reader.GetBoolean(3), createdAt = reader.GetDateTime(4) });
});

app.MapPost("/tareas", async (HttpContext ctx, NpgsqlDataSource db) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var input = JsonConvert.DeserializeObject<dynamic>(body)!;
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO tareas (titulo, descripcion) VALUES (@titulo, @desc) RETURNING id, titulo, descripcion, completada, created_at";
    cmd.Parameters.AddWithValue("titulo", (string)input.titulo);
    cmd.Parameters.AddWithValue("desc",   input.descripcion != null ? (string)input.descripcion : DBNull.Value);
    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    return Results.Created($"/tareas/{reader.GetGuid(0)}", new { id = reader.GetGuid(0), titulo = reader.GetString(1), descripcion = reader.IsDBNull(2) ? null : reader.GetString(2), completada = reader.GetBoolean(3), createdAt = reader.GetDateTime(4) });
});

app.MapPut("/tareas/{id}", async (Guid id, HttpContext ctx, NpgsqlDataSource db) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var input = JsonConvert.DeserializeObject<dynamic>(body)!;
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE tareas SET titulo=@titulo, descripcion=@desc, completada=@completada WHERE id=@id RETURNING id, titulo, descripcion, completada, created_at";
    cmd.Parameters.AddWithValue("id",          id);
    cmd.Parameters.AddWithValue("titulo",      (string)input.titulo);
    cmd.Parameters.AddWithValue("desc",        input.descripcion != null ? (string)input.descripcion : DBNull.Value);
    cmd.Parameters.AddWithValue("completada",  (bool)input.completada);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.NotFound(new { error = "Tarea no encontrada" });
    return Results.Ok(new { id = reader.GetGuid(0), titulo = reader.GetString(1), descripcion = reader.IsDBNull(2) ? null : reader.GetString(2), completada = reader.GetBoolean(3), createdAt = reader.GetDateTime(4) });
});

app.MapDelete("/tareas/{id}", async (Guid id, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM tareas WHERE id = @id";
    cmd.Parameters.AddWithValue("id", id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound(new { error = "Tarea no encontrada" }) : Results.Ok(new { message = "Tarea eliminada" });
});

app.Run();