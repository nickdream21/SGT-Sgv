# Pruebas pendientes

Lo que falta probar en el navegador, en un celular, con un pedido o un conductor real, o en el sitio publicado. Lo hace el agente `probador` (sitio local contra la BD de pruebas: `herramientas/sitio-local/README.md`, usuarios por rol en `privado/CREDENCIALES_USUARIOS.txt`, leídos desde un script) o el usuario cuando hace falta un conductor, un celular o un pedido real. En el **sitio publicado** el `probador` solo mira (nada de guardar, enviar, firmar ni borrar: tiene datos reales); lo que una prueba tenga que escribir allí (p. ej. restablecer la contraseña de una cuenta de prueba) lo hace o autoriza el usuario.

Prioridad: A = bloquea el uso real; B = flujo importante sin probar; C = menor. `[~]` = a medias.

## Con un pedido o un conductor real (usuario)

- [ ] **P-001** Prueba de punta a punta con un pedido real — correo + Word → carga → descarga → viaje → liquidación y firma → aprobación → PDF → contabilidad, en pruebas. · A
- [ ] **P-002** Ciclo de la liquidación con un conductor real — enviar (redirige a la firma y la auditoría `GUARDAR_LIQUIDACION` lo registra) → rechazar → reenviar (pide firma nueva; el PDF viejo sale ANULADO) → retirar una firmada. Después, M-014. · B
- [ ] **P-003** Celular real — Login, panel del conductor, Liquidaciones pendientes y Verificar documento; teclado numérico, coma decimal (M-069), barra fija del total, hoja de resumen y firma con el dedo en un equipo de gama baja; borrador de la liquidación (cerrar el navegador a mitad y volver); FirmarLiquidacion con una orden pendiente y la pestaña Liquidar con viaje activo (fechas `.dc-par`, que el aviso no tape `.dc-barra`); en Edge, el Buscar de Seguimiento no ofrece contraseñas y Enter filtra. · B
- [ ] **P-004** Destino de descarga con un pedido real — repartido entre Milagro y Jave: el lote se registra con una planta por tracto y `ListaDespachos` lo muestra como dos lotes. · C
- [ ] **P-005** Caso "por elegir" con programados reales — en la Bandeja y en RegistroDespacho (hoy hay 0 programados esperando documentos). · C

## En el sitio publicado (lo que escribe, con orden del usuario)

- [~] **P-006** Ver una vez el 503 real de `Salud.ashx?gps=1` — monitores de UptimeRobot creados y en verde ("SGV - Sitio y BD" y "SGV - GPS y Onway"); falta ver que el segundo marca el 503 (app arriba más de 60 min sin cron, o credenciales de Onway inválidas). Los `.so`/`.dylib` ya no se publican y los sobrantes se borraron en E1-5. · C
- [ ] **P-007** Aviso de errores real — provocar una vez un error en el sitio publicado y ver llegar el correo de `AvisoErrores`, y ver un aviso de postback parcial real (solo cubiertos por pruebas unitarias). Las claves SMTP y `SmtpFrom` ya están en el servidor (2026-11-20). · C
- [ ] **P-008** Recuperar contraseña en el sitio publicado — el enlace sale con `App.UrlPublica` y el restablecimiento funciona con un enlace válido (el correo ya llega a `admin.transporte`, id 90; falta usar el enlace). No usar la cuenta de una persona real. · C
- [ ] **P-009** Mapa, error remoto y autocompletado — mapa de SeguimientoViaje, un error remoto → `Error.aspx` y, con CONTABILIDAD, el autocompletado de conductor. · C
- [ ] **P-010** GPS automático: cuánto tarda en el sitio publicado — local: 1.ª corrida 49 viajes en 122 s; con viajes quietos, 20 s. Si somee corta las corridas largas, bajar `Presupuesto` en `ActualizacionGpsAutomatica`. Medir también "Actualizar GPS" con los viajes abiertos (cuántos alcanza por clic con la caché). · B
- [ ] **P-011** Envío de más de 25 MB — en somee podría salir "conexión restablecida" en vez del aviso. · C
- [ ] **P-023** Cronometrar una firma real en el hosting — para confirmar que las idas y vueltas a la BD desde somee son menores que desde la oficina (el arranque en frío ya lo evita UptimeRobot). · C

## En el sitio local (probador)

- [ ] **P-012** Seguimiento: lo que escribe — corregir una hora, guardar los datos que el GPS no sabe, editar datos del viaje, nuevo viaje, cerrar/reabrir, "Actualizar GPS", y los paneles de evidencia/corrección (JS). Por HTTP solo se probó lo que lee; las escrituras, con `SeguimientoViajesBdTests`. · B
- [ ] **P-013** Interfaz de documentos y documentos pendientes — registrar una factura y un CPIC de prueba desde la tarjeta, el paginado (página 2 con un filtro; que el pie no quede tapado por la columna fija de acciones) y el ancho de página; el recuadro, el filtro y "Documentos" del lote (adjuntar un PDF de factura y guardar una guía de prueba). · B
- [ ] **P-014** Reporte de cargas — pestañas, los dos gráficos (tooltip, ancho de celular) y que la pestaña se mantenga al buscar; solo se probó por HTTP. · C
- [ ] **P-015** Dashboard de exportación — clic en tramos y tooltips (probado por HTTP y con capturas sin interfaz). · C
- [ ] **P-016** Despacho en dos momentos — la vista previa (select2, avisos) y "Completa el despacho" al leer el ZIP. · C
- [ ] **P-017** Tablero del seguimiento con las cargas de mañana — y el formulario manual de RegistroDespacho plegado (probado solo por HTTP). · C
- [ ] **P-018** Bandeja: "La lectura venció" y reemplazo de un manifiesto — cubiertos por pruebas unitarias; faltaban datos para hacerlo sin escribir. · C
- [ ] **P-019** Diálogos de RegistroDespacho — Finalizar/Cancelar lote, Quitar y Reabrir con un lote de prueba en curso. · C
- [ ] **P-020** Gestión de Usuarios con `operador` — editar a `operador`, cancelar, "Nuevo usuario" → el desplegable no ofrece OPERADOR; editarlo sin cambiar el rol y guardar → lo conserva (escribe en la BD: solo si no se desactiva, U-015). · C
- [ ] **P-021** Cambio de contraseña con espacio final — por el modal del menú: nueva = actual + espacio → debe entrar escribiéndola sin el espacio; restaurarla después. · C
- [ ] **P-022** `Seguridad.ExigirPoliticaAlIngresar` en true — con una cuenta de prueba de contraseña débil (escribe datos de prueba). Cubierto por xUnit (`ExigeCambioAlIngresar`). · C

## Reglas

- Al pasar la prueba, el ítem se borra (si falla, el fallo pasa a `docs/PLAN_MEJORAS_SGV.md` como `M-`). Un pendiente nuevo toma el número siguiente al mayor de este documento.
- Evidencia de cada prueba en el informe del `probador` (tabla de casos, capturas a 1366 y 390 px).
