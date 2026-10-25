# PortPilot

Gestor de puertos para Windows 10/11. Abre **PortPilot.exe** con doble clic. No necesita instalar Python, Node ni paquetes; utiliza .NET Framework 4.8 de Windows. Conserva el archivo `PortPilot.exe.config` junto al ejecutable para el ajuste de escala.

## Interfaz

Diseño claro inspirado en Apple: superficies blancas, tarjetas redondeadas, tipografía uniforme, bordes suaves y azul para las acciones principales. El buscador, los selectores y los botones comparten una altura base de 40 px, ajustada por la escala de Windows. El contenido se centra y tiene un ancho máximo para conservar las proporciones al maximizar.

Los filtros están agrupados sobre la tabla. Los estados aparecen como etiquetas de color. El proceso seleccionado tiene su propia tarjeta con el botón **Liberar puerto…**. **Actualizar** y **Guía rápida** están en la cabecera; exportación y acceso de administrador, al pie.

Los selectores admiten ratón y teclado: Tab para enfocarlos, flechas para cambiar de opción y Home/End para ir al inicio/final. F1 abre la guía.

## Uso

1. Pulsa **Actualizar** o **F5** para consultar los puertos.
2. Busca por puerto, nombre de proceso, PID, dirección o ruta. **Ctrl+F** lleva al buscador. Filtra por tecnología y protocolo.
3. Selecciona una fila para ver la ruta, el comando de ejecución y todos los puertos del mismo proceso.
4. Pulsa **Liberar puerto…**, revisa el proceso y confirma su cierre. La opción predeterminada del diálogo es **No**.
5. Puedes activar la actualización cada cinco segundos o exportar las filas visibles a CSV.

## Indicadores de estado

Los colores siempre van acompañados de texto; no necesitas distinguir el color para entender el estado.

- **Verde · Activo:** el endpoint aparece en la consulta de Windows y la app permite intentar detener su proceso. No significa que el servicio funcione correctamente ni que el puerto esté libre.
- **Rojo · Inactivo:** un endpoint visto anteriormente dejó de aparecer. Se conserva durante cinco minutos desde que se detectó su desaparición, con un máximo de 500 registros recientes. El mismo puerto puede estar ocupado por otro proceso; no se garantiza que esté libre.
- **Ámbar · Activo / protegido:** el puerto está en uso, pero la app bloquea el cierre del proceso o servicio.
- **Gris · Activo / sin acceso:** el puerto aparece, pero faltan permisos o datos para identificar y cerrar su proceso con seguridad.

Los cuatro contadores muestran todos los endpoints antes de aplicar los filtros. Protegidos y sin acceso están incluidos en activos. Un número de puerto puede aparecer varias veces por protocolo, dirección o proceso.

Utiliza el filtro **Estado** para ver activos, inactivos, protegidos o sin acceso. Los registros inactivos nunca permiten cerrar procesos. El historial solo existe en memoria durante esta sesión y se actualiza al consultar de nuevo; no enumera todos los puertos libres del equipo. Una consulta fallida conserva los datos previos y muestra un aviso en rojo.

La franja bajo la tabla explica el estado de la fila seleccionada y la acción posible. Pasa el cursor por una fila para ver su explicación. **Guía rápida** incluye ejemplos de búsqueda y las definiciones de PID, TCP y UDP. El CSV incluye el estado y la hora de desaparición de los registros inactivos visibles.

Se identifican Node.js, Python, PHP, Nginx, Apache, .NET/IIS, Java, MySQL/MariaDB, PostgreSQL, Redis, MongoDB, Docker, WSL, Deno y Bun. La tecnología se clasifica según el nombre del ejecutable: un puerto 3000 no se supone automáticamente de Node. Programas empaquetados o con ejecutables renombrados pueden aparecer como **Otro**. El comando ayuda a reconocer el proyecto cuando Windows permite consultarlo.

## Qué significa liberar un puerto

Los puertos no se eliminan: se libera el recurso terminando el proceso que lo ocupa. El cierre es **forzado**: afecta a todos los puertos del mismo PID y puede perder trabajo no guardado. La app no termina procesos hijos automáticamente. Supervisores como PM2, Docker o servicios pueden reiniciar el proceso y volver a ocupar el puerto.

Se muestran TCP en escucha y endpoints UDP vinculados, tanto IPv4 como IPv6. UDP no tiene estado de escucha y también puede estar vinculado solo para enviar. Las conexiones TCP salientes y los puertos reservados o excluidos por Windows no forman parte de esta vista. Varias filas pueden compartir el mismo número de puerto.

La app bloquea el cierre de procesos de Windows, procesos críticos, servicios de sesión 0 y su propio proceso. Antes de cerrar verifica el instante de creación del proceso y que siga ocupando el endpoint. Los servicios como Nginx o bases de datos instalados como servicio deben detenerse desde su administrador de servicios. No modifica el firewall ni reservas de puertos.

Si faltan detalles o Windows deniega acceso, utiliza **Abrir como administrador** y acepta la solicitud de Windows si corresponde. La elevación es opcional y mantiene las protecciones. Algunas restricciones siguen aplicándose incluso como administrador. **Disponible** indica que la fila admite intentar el cierre; Windows puede denegar permisos al hacerlo.

Para Docker o WSL se ve el proceso de Windows que publica el puerto; no se inspeccionan los procesos internos de contenedores o Linux. Un PID 4 en HTTP puede corresponder a HTTP.sys: no se atribuye a una aplicación concreta.

La app consulta el equipo local. No envía información, no guarda historial en disco y no incluye telemetría. El historial reciente de estados se pierde al cerrar la app. El CSV guarda las filas filtradas y sus rutas; omite comandos de ejecución que podrían contener credenciales.

## Código y compilación

Incluye `PortPilot.cs`, `Interface.cs` (controles visuales) y `Compilar.ps1`. Desde PowerShell en esta carpeta:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1
```

Para compilar y ejecutar pruebas de integración:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1 -Pruebas
```

`Bypass` se aplica únicamente al proceso de compilación; no cambia la política guardada del sistema. Las pruebas crean un proceso propio con puertos de loopback TCP/UDP IPv4/IPv6, verifican la identificación, rechazan una identidad obsoleta y un endpoint inválido, y terminan exclusivamente ese proceso de prueba. No requieren Internet. El cierre se omite y se informa como prueba incompleta si se ejecutan en una sesión de servicios.

El ejecutable es una compilación local sin firma de editor.
