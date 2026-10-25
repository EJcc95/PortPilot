# PortPilot

**Encuentra qué ocupa tus puertos y recupera tu entorno de desarrollo.**

Aplicación de escritorio portable para **Windows 10 y 11**, escrita en **C# y Windows Forms**. Identifica procesos como Node.js, Python, PHP y Nginx, muestra sus puertos TCP/UDP y permite intentar liberarlos deteniendo el proceso responsable, con confirmación.

La interfaz está en español y utiliza un diseño claro inspirado en Apple: controles uniformes, tarjetas, filtros y etiquetas de estado con colores y texto.

**[Descargar para Windows](https://github.com/EJcc95/PortPilot/releases/latest)** · **[Guía de uso](docs/GUIA_USO.md)** · **[Documentación técnica](docs/ARQUITECTURA.md)** · **[Pruebas](docs/PRUEBAS.md)**

## Qué puedes hacer

- Consultar puertos TCP en escucha y endpoints UDP, tanto IPv4 como IPv6.
- Buscar por puerto, proceso, PID, dirección o ruta del ejecutable.
- Filtrar por tecnología, protocolo y estado.
- Consultar la ruta, el comando y los puertos del proceso seleccionado.
- Liberar un puerto terminando el proceso responsable, después de confirmar.
- Ver registros que dejaron de aparecer durante los últimos cinco minutos.
- Activar la actualización automática cada cinco segundos.
- Exportar las filas visibles a CSV.

Tecnologías reconocidas por nombre de ejecutable: **Node.js, Python, PHP, Nginx, Apache, .NET/IIS, Java, MySQL/MariaDB, PostgreSQL, Redis, MongoDB, Docker, WSL, Deno y Bun**. Los ejecutables que no coincidan con estas reglas se muestran como **Otro**.

## Empezar en tres pasos

1. Entra a **[Releases](https://github.com/EJcc95/PortPilot/releases/latest)** y descarga `PortPilot-Windows.zip` desde **Assets**.
2. Extrae todo el ZIP en una carpeta de tu equipo.
3. Abre `PortPilot.exe` con doble clic. Mantén `PortPilot.exe.config` junto al ejecutable.

No necesitas instalar Python, Node, Visual Studio ni paquetes NuGet para utilizar la app. Requiere **.NET Framework 4.8**. Si tu instalación de Windows no lo tiene habilitado, utiliza el instalador oficial de Microsoft o la configuración de componentes de Windows.

El ejecutable se distribuye sin firma de editor. No cambies ni desactives la protección de Windows para abrirlo: revisa el origen del archivo o compílalo desde el código si lo prefieres.

## Paso a paso: encontrar y liberar un puerto

Ejemplo: tu proyecto indica que el puerto `3000` está ocupado.

1. Abre PortPilot y pulsa **Actualizar** o **F5**.
2. Escribe `3000` en el buscador. Si no aparece, deja **Estado: Todos** y **Protocolo: Todos** y vuelve a actualizar.
3. Selecciona la fila y revisa el nombre, el PID y la ruta del proceso en la tarjeta inferior.
4. Comprueba que corresponde al proyecto que quieres cerrar. Un número de puerto por sí solo no identifica su tecnología.
5. Si el proceso permite el cierre, pulsa **Liberar puerto…**.
6. Lee la confirmación: se terminará el proceso completo y se cerrarán **todos sus puertos**, no solo el seleccionado.
7. Confirma únicamente si deseas detener ese proceso. El valor predeterminado del diálogo es **No**.
8. Comprueba el resultado y vuelve a iniciar tu proyecto.

> El cierre es forzado: el proceso puede perder trabajo no guardado. Un supervisor como PM2 o Docker puede reiniciarlo y volver a ocupar el puerto. Los procesos hijos no se terminan automáticamente.

## Entender los colores

| Indicador | Significado | Qué puedes hacer |
| --- | --- | --- |
| 🟢 Activo | El endpoint aparece en la consulta de Windows. | Revisar sus detalles e intentar detener el proceso si está permitido. |
| 🔴 Inactivo | Un registro observado anteriormente dejó de aparecer. | Consultarlo como referencia; no permite cerrar procesos. |
| 🟠 Activo · protegido | El puerto está en uso y el cierre está bloqueado por la app. | Utilizar el administrador correspondiente, como Servicios de Windows. |
| ⚪ Activo · sin acceso | El puerto aparece, pero faltan permisos o datos del proceso. | Actualizar o abrir la app como administrador si corresponde. |

**Activo no significa que el servicio responda correctamente. Inactivo no garantiza que el puerto esté libre:** otro proceso puede utilizarlo. El historial conserva hasta 500 registros desaparecidos durante cinco minutos, solo en la sesión actual.

Los contadores muestran todos los registros antes de filtrar. Protegidos y sin acceso están incluidos en activos. Un mismo número de puerto puede aparecer varias veces por protocolo, dirección o proceso.

## Atajos

| Atajo | Acción |
| --- | --- |
| F5 | Actualizar |
| Ctrl+F | Buscar |
| F1 | Abrir la guía rápida |
| Tab | Moverse entre controles |
| Flechas / Home / End | Cambiar la opción de un filtro enfocado |

## Compilar desde el código

### 1. Descargar el proyecto

Con Git:

```powershell
git clone https://github.com/EJcc95/PortPilot.git
cd PortPilot
```

También puedes elegir **Code → Download ZIP** en GitHub y extraerlo.

### 2. Compilar

Abre PowerShell en la raíz del proyecto y ejecuta:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1
```

El script utiliza el compilador de .NET Framework de Windows y genera:

```text
dist/
├── PortPilot.exe
└── PortPilot.exe.config
```

`ExecutionPolicy Bypass` se aplica solamente a ese proceso; no modifica la política guardada del sistema. No hay dependencias NuGet ni restauración de paquetes.

### 3. Ejecutar

```powershell
.\dist\PortPilot.exe
```

### 4. Ejecutar las pruebas de integración

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1 -Pruebas
```

Las pruebas crean un proceso propio con puertos de loopback TCP/UDP IPv4/IPv6. Verifican detección, clasificación, historial, protección contra datos desactualizados y cierre exclusivo de ese proceso. La validación local documentada pasó **17 comprobaciones**.

La prueba de cierre requiere una sesión de usuario interactiva. Si se ejecuta en sesión 0 o en un entorno que impide consultar puertos, puede informar una prueba incompleta. Esto no equivale a una validación exitosa. Consulta [PRUEBAS.md](docs/PRUEBAS.md) para conocer el alcance y las limitaciones.

### 5. Crear un ZIP portable

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Crear-Paquete.ps1
```

El resultado queda en `artifacts/PortPilot-Windows.zip`, con el ejecutable, su configuración y la documentación. El script vuelve a compilar antes de empaquetar.

## Compilación en GitHub Actions

El flujo **Compilar Windows** compila la app y genera el paquete en cada push a `main`, en los pull requests y mediante ejecución manual. El ZIP queda como artefacto de la ejecución.

Este flujo comprueba **compilación y empaquetado**; no ejecuta la prueba de terminación de procesos en el servidor de CI. Las descargas estables se publican por separado en Releases.

## Estructura del repositorio

```text
PortPilot/
├── src/
│   ├── PortPilot.cs              # Motor de puertos, historial, interfaz y pruebas
│   ├── Interface.cs              # Controles visuales personalizados
│   └── PortPilot.exe.config      # .NET Framework y escala DPI
├── docs/
│   ├── GUIA_USO.md               # Uso detallado
│   ├── ARQUITECTURA.md           # Funcionamiento y decisiones técnicas
│   └── PRUEBAS.md                # Validación realizada y límites
├── .github/workflows/build.yml   # Compilación en Windows
├── Compilar.ps1
├── Crear-Paquete.ps1
├── CONTRIBUTING.md
├── CHANGELOG.md
└── README.md
```

Los directorios generados `dist/` y `artifacts/` están excluidos de Git. El código se conserva en el repositorio y el ejecutable se entrega como descarga en Releases.

## Límites y comportamiento

- Muestra **TCP en escucha** y **UDP vinculado**; no enumera todas las conexiones TCP salientes ni todos los puertos disponibles.
- No modifica firewall, reservas de puertos ni exclusiones de Windows.
- Bloquea procesos de Windows, procesos críticos, servicios de sesión 0 y el propio PortPilot.
- Nginx o bases de datos instalados como servicio deben detenerse desde su administrador de servicios.
- Docker y WSL muestran el proceso de Windows que publica el puerto; no se inspeccionan sus procesos internos.
- La identificación depende del nombre del ejecutable. Programas renombrados o aplicaciones .NET autónomas pueden aparecer como **Otro**.
- Antes de terminar un proceso se verifica su fecha de creación, que siga ocupando el endpoint y que no sea crítico. Windows puede denegar el cierre por permisos.

## Privacidad

Las consultas se realizan localmente. La app no tiene telemetría, no envía información a terceros y no guarda historial en disco. El CSV contiene las filas visibles y sus rutas; omite comandos de ejecución que podrían incluir datos sensibles. Si compartes un CSV, revisa su contenido primero.

## Solución de problemas

| Situación | Qué revisar |
| --- | --- |
| No aparece un puerto | Pulsa F5 y limpia los filtros. Puede estar reservado, ser una conexión saliente o pertenecer al entorno Linux/contenedor. |
| No aparecen la ruta o el comando | Windows puede restringir esos datos. La elevación opcional puede ayudar, pero no elimina todas las restricciones. |
| Liberar puerto está deshabilitado | Selecciona un registro activo que no esté protegido y tenga datos suficientes. |
| El puerto vuelve a ocuparse | Revisa supervisores, contenedores o servicios que reinicien el proceso. |
| Error de compilación por archivo en uso | Cierra la instancia de `dist/PortPilot.exe` y compila de nuevo. |
| No se encuentra `csc.exe` | Comprueba que .NET Framework 4.8 esté disponible en Windows. |

Para informar de un problema, abre un [issue](https://github.com/EJcc95/PortPilot/issues) con los pasos para reproducirlo y la versión de Windows. Evita adjuntar credenciales o comandos con secretos. Lee [CONTRIBUTING.md](CONTRIBUTING.md) antes de proponer cambios.

## Autor

Proyecto de **[Ec-Dev / EJcc95](https://github.com/EJcc95)**.
