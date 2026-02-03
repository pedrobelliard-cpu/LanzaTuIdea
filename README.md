# LanzaTuIdea (INFOTEP) - MVP Auth + Roles

## Requisitos
- .NET 8 SDK
- SQL Server LocalDB (para desarrollo)

## Configuración rápida
1. Ajusta el endpoint del servicio SOAP si aplica:
   - `appsettings.json` → `AdService:BaseUrl`
2. (Opcional) agrega un seed de empleados:
   - Copia `seed/empleados.sample.csv` a `seed/empleados.csv` y completa los datos.
3. (Opcional) define bootstrap admins en desarrollo:
   - `BootstrapAdmins: ["usuario1", "usuario2"]`

## Cómo correr en desarrollo
```bash
dotnet run
```

En Development la app:
- Aplica migraciones automáticamente.
- Crea roles base (Admin, Ideador).
- Si existe `seed/empleados.csv`, lo importa cuando la tabla está vacía.

## Flujos principales
- **Login**: usa el servicio SOAP real.
- **Ideador**: registra ideas y consulta su historial.
- **Admin** (solo si tiene rol Admin): revisa ideas, clasifica y registra manualmente.

## Checklist de pruebas manuales
1. Inicia sesión con un usuario válido.
2. Verifica que aparece la vista de ideador y el empleado está precargado.
3. Registra una idea y revisa la lista “Mis Ideas Lanzadas”.
4. (Admin) Accede al dashboard y a los inbox de ideas pendientes y revisadas.
5. (Admin) Actualiza estatus/clasificación desde el modal.
6. (Admin) Registra una idea manual y confirma que aparece en el dashboard.
