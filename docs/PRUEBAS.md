# Validación de esta entrega

Compilación local correcta con el compilador de .NET Framework de Windows.

El rediseño visual también compila correctamente. Se repitieron las diecisiete comprobaciones funcionales. Se revisaron controles de altura uniforme, contadores, etiquetas de estado, detalles y botones. Se probó seleccionar **Inactivos** con el ratón (sin coincidencias) y volver a **Todos** con Home. Durante esta revisión se detectó y corrigió la liberación prematura del menú emergente antes de completar el evento de selección; la selección se volvió a probar con éxito.

Se revisó la ventana en tamaño normal y maximizada en la pantalla del equipo. En ambos casos las tarjetas y los controles permanecieron alineados. Al maximizar, el ancho máximo conservó el contenido centrado y las columnas Protocolo, Tecnología y Dirección local mantuvieron sus cabeceras completas. No se verificaron configuraciones de varios monitores ni todas las escalas DPI de Windows.

Diecisiete comprobaciones correctas:

1. Clasificación de Node.js, Python, PHP y Nginx.
2. Ejecutables desconocidos clasificados como Otro.
3. Exportación CSV con escape de comillas y neutralización de fórmulas.
4. Bloqueo de procesos de Windows y servicios de sesión 0.
5. Detección de listeners TCP IPv4 e IPv6 de un proceso de prueba.
6. Detección de endpoints UDP IPv4 e IPv6 del mismo proceso.
7. Obtención de su identidad y fecha de creación.
8. Rechazo de una identidad de proceso desactualizada.
9. Rechazo de un endpoint que no pertenece al proceso.
10. Cierre exclusivo del proceso creado para la prueba y comprobación de que desaparecieron sus puertos.
11. Un registro observado aparece activo y permite intentar el cierre.
12. Un registro que desaparece queda inactivo y sin acción de cierre.
13. El motor rechaza registros inactivos antes de abrir un proceso.
14. Reocupación del mismo puerto por otro PID sin mezclar estados.
15. Reaparición del mismo endpoint sin duplicar su identidad.
16. Caducidad de registros anteriores a cinco minutos.
17. Eliminación del historial caducado sin inventar puertos libres.

Revisión visual de la app: tabla con datos reales, selección y detalles, contadores y leyenda de colores. Se comprobó el ámbar y el botón de cierre deshabilitado para un proceso de sesión 0. Un proceso temporal propio permitió comprobar los indicadores verdes TCP/UDP en IPv4/IPv6 y su cambio automático a rojo cuando terminó normalmente. Los registros rojos mostraron la hora de desaparición y el botón de cierre deshabilitado. Se comprobó la apertura del selector de estado. No se terminaron servidores ni procesos existentes del usuario.

No se probaron la elevación mediante UAC, el diálogo de Guía rápida, todas las combinaciones de filtros ni el guardado desde el diálogo de exportación. La escritura CSV se revisó en código y su escape se probó automáticamente. La detección de tecnologías se validó por nombre; no se iniciaron servidores de todas las tecnologías enumeradas.
