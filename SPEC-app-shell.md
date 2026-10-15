# Spec: app-shell (módulo 1 de 7)

**Estado:** borrador, pendiente de aprobación. Mapa: [CAPABILITY-MAP.md](CAPABILITY-MAP.md). Decisiones de base: ADR-001 (stack), ADR-003 (logs sin URI ni argumentos) y ADR-005 (sin firma de código, licencia MIT).

## Objetivo

Construir la base sobre la que se montan los demás módulos: un repositorio que compila y pasa los tests en CI, y una aplicación WPF que arranca, se ve, recuerda sus ajustes, vive en la bandeja del sistema y no se abre dos veces.

**Usuario:** el propietario del proyecto, en su PC con Windows 11 Home. Más adelante, cualquier usuario de Windows 10/11 x64 sin permisos de administrador.

**Fuera de este módulo:** todo lo relacionado con Roblox (descargar, lanzar, instancias). La navegación **solo muestra secciones que funcionan**: este módulo entrega «Inicio» y «Ajustes». Las demás se añaden cuando exista su módulo.

### Historias de usuario
1. Como usuario, abro LarpStrap y veo una ventana con el nombre y la versión de la app, y una navegación con «Inicio» y «Ajustes».
2. Como usuario, si abro LarpStrap otra vez con la ventana ya abierta, no aparece una segunda ventana: la existente se restaura y pasa al frente.
3. Como usuario, elijo el tema (sistema, claro u oscuro) y la opción «cerrar a la bandeja», y se mantienen al reiniciar.
4. Como usuario, si cierro la ventana con «cerrar a la bandeja» activado, LarpStrap sigue en la bandeja. El menú de la bandeja tiene «Abrir» y «Salir».
5. Como mantenedor, tengo un log en disco que nunca contiene URIs ni líneas de comandos.

## Stack

| Pieza | Versión |
|---|---|
| .NET SDK | 10.0.401 (instalado), `net10.0-windows`, fijado con `global.json` |
| UI | WPF (`UseWPF`) + Windows Forms solo para `NotifyIcon` de la bandeja (`UseWindowsForms`) |
| MVVM | CommunityToolkit.Mvvm 8.4.x |
| Win32 | Microsoft.Windows.CsWin32 0.3.x (traer la ventana al frente, `FlashWindowEx`) |
| Tests | xUnit (v3 si su integración con `dotnet test` en .NET 10 lo permite; se verifica en la documentación durante el plan) |
| CI | GitHub Actions, `windows-latest` |

Las versiones exactas de los paquetes se fijarán en el plan tras comprobarlas en NuGet y en la documentación oficial (skill source-driven-development).

## Comandos

```
Restaurar:       dotnet restore LarpStrap.slnx
Compilar:        dotnet build LarpStrap.slnx -c Release
Ejecutar:        dotnet run --project src/LarpStrap.App
Todos los tests: dotnet test LarpStrap.slnx -c Release
Un test:         dotnet test tests/LarpStrap.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"
Formato:         dotnet format LarpStrap.slnx --verify-no-changes
Publicar:        dotnet publish src/LarpStrap.App -c Release -r win-x64 --self-contained
```

## Estructura del proyecto

```
LarpStrap.slnx
global.json                 SDK 10.0.4xx
Directory.Build.props       net10.0-windows, Nullable, TreatWarningsAsErrors, LangVersion latest
.editorconfig  .gitignore
src/LarpStrap.Core/         lógica sin UI
  Settings/                 AppSettings (record), SettingsStore (JSON, escritura atómica)
  Logging/                  AppLog: archivo con rotación; solo acepta mensajes de lista blanca
  SingleInstance/           InstanceGuard (mutex Local\) + PipeChannel (named pipe del usuario actual)
  Paths.cs                  %LOCALAPPDATA%\LarpStrap\{Settings.json, Logs\}
src/LarpStrap.App/          WPF
  App.xaml(.cs)             arranque: guard → settings → ventana → bandeja
  Shell/                    MainWindow, navegación (Inicio, Ajustes)
  Settings/                 SettingsView + SettingsViewModel
  Tray/                     TrayIcon (NotifyIcon)
  Themes/                   tokens de color y estilos propios, claro y oscuro
tests/LarpStrap.Core.Tests/ xUnit
docs/  CONSTRAINTS.md  README.md
.github/workflows/ci.yml    build + test + formato
```

## Estilo de código

- El código y los comentarios van en inglés. La interfaz y la documentación, en español.
- Namespaces de ámbito de archivo, `sealed` por defecto, `record` para datos y `CancellationToken` en toda operación asíncrona.
- Sin estado estático mutable. Las dependencias se pasan por constructor, sin contenedor de inyección de dependencias hasta que haga falta.
- Los comentarios explican el *porqué*. Cuando se simplifica algo a propósito, se marca con `// ponytail:` indicando su límite.

```csharp
namespace LarpStrap.Core.Settings;

public sealed record AppSettings(ThemeMode Theme = ThemeMode.System, bool CloseToTray = true);

public sealed class SettingsStore(string path)
{
    public AppSettings Load()
    {
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.AppSettings)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Keep the broken file for diagnosis; never crash on startup because of settings.
            File.Copy(path, path + ".bad", overwrite: true);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, SettingsJson.Default.AppSettings));
        File.Move(tmp, path, overwrite: true); // atomic replace on the same volume
    }
}
```

## Estrategia de pruebas

| Qué | Cómo | Tipo |
|---|---|---|
| `SettingsStore`: ida y vuelta, archivo inexistente, JSON corrupto (se crea `.bad` y se cargan los valores por defecto), escritura atómica | xUnit con directorio temporal | Simulada |
| `AppLog`: rotación (máximo 10 archivos); un mensaje con una URI o argumentos ficticios no llega al disco | xUnit | Simulada |
| `InstanceGuard` + `PipeChannel`: el segundo proceso detecta al primero y le envía «activate»; el pipe rechaza a otros usuarios (`PipeOptions.CurrentUserOnly`) | xUnit con dos guards en el mismo proceso, más un test que arranca un proceso hijo de prueba | Simulada |
| Ventana, bandeja, tema, foco, accesibilidad por teclado | Checklist manual en `docs/testing/manual-app-shell.md` | Manual (no necesita Roblox) |

Cobertura: no hay porcentaje objetivo. Toda la lógica de `Core` tiene al menos un test que fallaría si se rompe. La interfaz se comprueba con el checklist manual.

## Límites

- **Siempre:**
  - compilar y pasar los tests antes de cada commit;
  - mantener 0 warnings;
  - escribir los archivos de configuración de forma atómica;
  - registrar en el log solo mensajes de la lista blanca.
- **Preguntar antes:**
  - añadir paquetes NuGet distintos de los listados;
  - cambiar el workflow de CI;
  - escribir en el registro de Windows (este módulo no lo necesita).
- **Nunca:**
  - registrar URIs, líneas de comandos ni datos de cuentas;
  - mostrar en la navegación secciones sin funcionalidad;
  - tocar nada de Roblox en este módulo.

## Criterios de éxito

1. `dotnet build LarpStrap.slnx -c Release` termina con 0 warnings y 0 errores, en local y en CI.
2. `dotnet test` en verde, en local y en CI. `dotnet format --verify-no-changes` en verde.
3. Al ejecutar la app aparece la ventana con nombre, versión e «Inicio» y «Ajustes» en la navegación, y nada más.
4. Si se ejecuta el exe por segunda vez, no aparece otra ventana y la existente pasa al frente. Si Windows bloquea el primer plano, la ventana parpadea en la barra de tareas.
5. El tema (sistema, claro u oscuro) y «cerrar a la bandeja» se mantienen tras reiniciar. Con «sistema», la app sigue el modo claro u oscuro de Windows.
6. Con «cerrar a la bandeja» activado, cerrar la ventana deja el icono en la bandeja. «Abrir» la restaura y «Salir» termina el proceso.
7. Si `Settings.json` está corrupto, la app arranca con los valores por defecto, guarda `Settings.json.bad` y muestra un aviso.
8. Se puede navegar por toda la interfaz con el teclado, el foco siempre es visible y el contraste cumple WCAG AA en ambos temas (checklist manual).
9. Arranque en frío por debajo de 2 s en el PC de desarrollo (medición manual, que se anotará en el checklist).

## Preguntas abiertas

1. **Identidad visual:** ¿la defino ya en este módulo (paleta, logotipo, tipografía) o pongo ahora un tema neutro con tokens y hago la identidad al final? Recomiendo el tema neutro ahora, para no rediseñar la interfaz cuando existan las demás pantallas.
2. **Autor del copyright y del log de versiones:** se usará «Kaiz-2», como en `LICENSE`, salvo que indiques otro nombre.
