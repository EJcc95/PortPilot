# Contribuir a PortPilot

## Informar de un problema

Abre un issue e incluye:

- Versión de Windows y de PortPilot.
- Pasos concretos para reproducir el problema.
- Resultado esperado y resultado observado.
- Si la app se ejecutó como usuario estándar o administrador.
- Captura del error si es útil, ocultando rutas privadas, comandos con credenciales y otros datos personales.

## Preparar el entorno

1. Trabaja en Windows 10/11 con .NET Framework 4.8.
2. Clona el repositorio o descarga el código.
3. Ejecuta `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1`.
4. Abre `dist/PortPilot.exe` y comprueba el comportamiento.
5. Para cambios en el motor o historial, ejecuta también `Compilar.ps1 -Pruebas` desde una sesión de usuario interactiva.

## Proponer cambios

1. Describe el problema en un issue cuando requiera discusión.
2. Crea una rama con un nombre que describa el cambio.
3. Mantén los cambios centrados en el problema propuesto.
4. Actualiza la documentación si cambia el comportamiento.
5. Abre un pull request con el resultado, la validación realizada y sus limitaciones.

No incluyas los directorios `dist/`, `artifacts/`, archivos CSV del equipo, credenciales ni ejecutables generados en el historial de código.

## Criterios de revisión

- Mantener la verificación de identidad del proceso antes de terminarlo.
- Mantener bloqueados procesos críticos, de sistema y de sesión 0.
- No permitir el cierre desde un registro inactivo.
- No confundir un registro inactivo con un puerto garantizado como libre.
- Mantener texto además de color en los estados y navegación por teclado.
- Evitar consultas largas en el hilo de la interfaz.
- Documentar las comprobaciones realizadas sin afirmar validaciones que no se ejecutaron.
