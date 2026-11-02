-- ============================================================
-- Tracto 2 y conductor de descarga de los viajes programados el 30/10 y el 31/10/2026
-- (STATUS GENERAL los mostraba vacios desde el 30/10). Salen de las secciones GUAYAQUIL-VITAPRO
-- de DESPACHO 30-10-26 y 01-11-26 (columnas CHOFER / TRACTO / CARRETA), unidas por la carreta del viaje.
-- Solo completa viajes sin tracto 2 ni chofer de descarga; no toca pedido ni planta de descarga.
-- Cuando se suban los ZIP y se registre la descarga de cada pedido, sp_SE_SincronizarDespachos
-- pone el mismo tracto/chofer desde el despacho de DESCARGA (y el id del despacho).
-- Re-ejecutable (un viaje ya completado no cambia).
-- ============================================================
SET NOCOUNT ON;

DECLARE @lista TABLE (carreta VARCHAR(20), tracto1 VARCHAR(20), tracto2 VARCHAR(20), chofer VARCHAR(150));
INSERT INTO @lista VALUES
    (N'T9U-996', N'CBU-930', N'CBU-930', N'LUIS JULCA'),  -- Word 30-10-26
    (N'AVK-988', N'ABJ-805', N'ABJ-805', N'JOSE TALLEDO'),  -- Word 01-11-26
    (N'TBE-995', N'AVN-717', N'AVN-717', N'HENRY PINEDA'),  -- Word 01-11-26
    (N'AVG-990', N'ARN-913', N'ARN-913', N'CARLOS MONTERO'),  -- Word 01-11-26
    (N'T0C-998', N'T6F-864', N'BNH-869', N'LUIS PONCE'),  -- Word 30-10-26
    (N'T2G-972', N'T7J-860', N'AVO-818', N'ORESTES CHAMBA'),  -- Word 30-10-26
    (N'TMB-986', N'BPT-919', N'BPT-919', N'RENAN BARBA'),  -- Word 01-11-26
    (N'AUK-976', N'BFI-890', N'BFI-890', N'FRANKLIN NAVARRO'),  -- Word 01-11-26
    (N'T6R-976', N'BSD-716', N'BSD-716', N'PRIER SILVA'),  -- Word 30-10-26
    (N'T4G-982', N'ANU-794', N'CBV-854', N'ROBERTO MACALUPU'),  -- Word 30-10-26
    (N'T4G-975', N'BFR-730', N'BFR-730', N'CRISTHIAN YUNGA'),  -- Word 01-11-26
    (N'T0C-999', N'AVN-709', N'AVN-709', N'NOLBERTO SUNCIÓN'),  -- Word 30-10-26
    (N'T6P-989', N'BNH-712', N'BNH-712', N'AGUSTO ALCANTARA'),  -- Word 01-11-26
    (N'T9O-987', N'BPT-788', N'CBU-945', N'WILSON CORREA'),  -- Word 01-11-26
    (N'TMB-988', N'BNG-862', N'T8V-933', N'MIGUEL FARFAN'),  -- Word 01-11-26
    (N'T4C-979', N'BPS-816', N'T7J-861', N'DANILO PIEDRA'),  -- Word 01-11-26
    (N'T4L-975', N'TBM-815', N'TBM-815', N'JHONATAN HUAMAN'),  -- Word 01-11-26
    (N'T4Q-984', N'BNH-865', N'AVN-758', N'GAMANIEL HUAMAN'),  -- Word 01-11-26
    (N'AUK-997', N'BFI-817', N'BFH-852', N'ESTEBAN CASTRO'),  -- Word 01-11-26
    (N'T6P-997', N'TBM-898', N'AVM-877', N'ANYELO CURAY'),  -- Word 01-11-26
    (N'T5F-972', N'CBU-928', N'CBU-928', N'FRANK RIVERA'),  -- Word 01-11-26
    (N'TEC-991', N'C6K-799', N'CBV-829', N'JOEL RAMIREZ')   -- Word 01-11-26;

UPDATE se SET tracto2 = l.tracto2, conductorDestino = l.chofer, fechaModificacion = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion se
JOIN @lista l ON l.carreta = se.carreta AND l.tracto1 = se.tracto1
WHERE se.activo = 1 AND se.tipoRuta IS NULL AND se.tracto2 IS NULL AND se.conductorDestino IS NULL
  AND se.fhProgramacion >= '2026-10-30' AND se.fhProgramacion < '2026-11-01';

SELECT @@ROWCOUNT AS viajes_completados;