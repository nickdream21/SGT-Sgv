-- El sobre ya no registra quién lo llevó (2026-11-11).
--
-- La migración 42 exigía, en las entregas por SOBRE, el conductor que lo llevaba
-- (idConductorPortador). Se deja de pedir: la gerencia sabe cuánto entregó y el conductor que lo
-- recibe lo reconoce al firmar su conformidad con la liquidación final, donde figura cada entrega
-- con su fecha, concepto, medio y monto. Nada se le asigna sin que lo acepte al firmar, así que
-- el nombre de un tercero no agrega sustento y sí lo involucra en un documento ajeno.
--
-- Aquí solo se quita la restricción que obligaba a llenarlo. La columna (y su FK) se conserva
-- vacía mientras el sitio publicado siga leyéndola; se borrará en una migración posterior, una
-- vez publicado el código que ya no la usa (pendiente anotado en docs/PLAN_MEJORAS_SGV.md).
IF OBJECT_ID('CK_MovimientoDineroViaje_Sobre', 'C') IS NOT NULL
    ALTER TABLE MovimientoDineroViaje DROP CONSTRAINT CK_MovimientoDineroViaje_Sobre;
GO
