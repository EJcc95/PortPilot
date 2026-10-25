# Historial de versiones

## 1.0.0

Primera publicación pública de PortPilot.

### Funcionalidad

- Consulta local de TCP en escucha y UDP vinculado en IPv4/IPv6.
- Identificación por ejecutable de tecnologías de desarrollo y servidores.
- Búsqueda por puerto, proceso, PID, dirección y ruta.
- Filtros por tecnología, protocolo y estado.
- Cierre con confirmación, validación de identidad y protección de procesos.
- Historial de inactivos de cinco minutos, limitado a 500 registros en memoria.
- Actualización automática cada cinco segundos y exportación a CSV.

### Interfaz

- Diseño claro inspirado en Apple, con contenido centrado y tarjetas redondeadas.
- Controles de altura uniforme y estados con color y texto.
- Tarjeta independiente para el proceso seleccionado.
- Guía rápida, atajos y selectores con navegación por teclado.

### Entrega

- Código completo sin dependencias NuGet.
- Scripts de compilación y empaquetado.
- Documentación en español y registro de 17 comprobaciones locales.
- Flujo de GitHub Actions para compilar y producir el paquete.

### Limitaciones conocidas

- No administra servicios de Windows ni procesos internos de Docker o WSL.
- El cierre es forzado y no termina automáticamente procesos hijos.
- El ejecutable no tiene firma de editor.
- La prueba de cierre requiere sesión de usuario y acceso a las tablas de puertos.
- No se ha validado toda combinación de escalas DPI y varios monitores.
