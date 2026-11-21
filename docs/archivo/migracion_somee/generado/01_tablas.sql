-- ============================================================================
--  SGT-SGV  Â·  Migracion a somee Pro  Â·  Generado automaticamente el 2026-07-08 09:33
--  Origen: sgvActualizada (somee)  Â·  NO editar a mano salvo necesidad.
--  Ejecutar CONECTADO A LA NUEVA BASE DE PRODUCCION.
-- ============================================================================
-- 01 Â· ESTRUCTURA DE TABLAS (69 tablas; excluidas las 2 de backup)
SET NOCOUNT ON;
GO
IF OBJECT_ID('dbo.AbastecimientoCombustible', 'U') IS NULL
CREATE TABLE [dbo].[AbastecimientoCombustible] (

     [idAbastecimientoCombustible] INT IDENTITY(1,1) NOT NULL
,    [numeroAbastecimientoCombustible] CHAR(6) NOT NULL
,    [idTracto] INT NULL
,    [idConductor] INT NULL
,    [producto] VARCHAR(100) NOT NULL
,    [idLugarAbastecimiento] INT NULL
,    [fechaHora] DATETIME NOT NULL
,    [galonesRutaAsignada] DECIMAL(11,2) NOT NULL
,    [galonesCompradosRuta] DECIMAL(11,2) NOT NULL
,    [galonesTotalAbastecidos] DECIMAL(11,2) NOT NULL
,    [galonesAlFinalizar] DECIMAL(11,2) NOT NULL
,    [galonesTotalConsumidos] DECIMAL(11,2) NOT NULL
,    [precioDolar] DECIMAL(11,3) NULL
,    [montoTotalGalonesComprados] DECIMAL(11,2) NOT NULL
,    [distanciaRutaKM] DECIMAL(11,2) NOT NULL
,    [consumoComputador] DECIMAL(11,2) NOT NULL
,    [observaciones] VARCHAR(300) NULL
,    [horaRetorno] TIME(7) NULL
,    [idTipoCarro] INT NULL
,    [idCarreta] INT NULL
,    [idRuta] INT NULL
,    [rendimientoPromedio] DECIMAL(11,2) NULL
,    [idOrdenViaje] INT NULL
,    [rutaDescripcion] VARCHAR(500) NULL
,    [tipoAbastecimiento] VARCHAR(50) NULL CONSTRAINT [DF__Abastecim__tipoA__48BAC3E5] DEFAULT ('ABASTECIMIENTO')
,    [idVolquete] INT NULL
,    [idCamioneta] INT NULL
,    [rutaPdfGenerado] VARCHAR(500) NULL
,    [hashPdfGenerado] CHAR(64) NULL
,    [fechaGeneracionPdf] DATETIME NULL
,    CONSTRAINT [PK__Abasteci__E6C9E58D927A97DE] PRIMARY KEY CLUSTERED ([idAbastecimientoCombustible] ASC)
,    CONSTRAINT [UQ__Abasteci__DF8F03E5EC45E000] UNIQUE NONCLUSTERED ([numeroAbastecimientoCombustible] ASC)
);
GO

IF OBJECT_ID('dbo.AsignacionesMaquinaria', 'U') IS NULL
CREATE TABLE [dbo].[AsignacionesMaquinaria] (

     [idAsignacion] INT IDENTITY(1,1) NOT NULL
,    [idOperador] INT NOT NULL
,    [idEquipo] INT NOT NULL
,    [idObra] INT NOT NULL
,    [fechaAsignacion] DATE NOT NULL
,    [fechaFinAsignacion] DATE NULL
,    [estado] VARCHAR(20) NOT NULL CONSTRAINT [DF__Asignacio__estad__5EAA0504] DEFAULT ('ACTIVA')
,    [observaciones] VARCHAR(500) NULL
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__Asignacio__fecha__5F9E293D] DEFAULT (getdate())
,    CONSTRAINT [PK__Asignaci__E1714478B657AFC0] PRIMARY KEY CLUSTERED ([idAsignacion] ASC)
);
GO

IF OBJECT_ID('dbo.Auditoria', 'U') IS NULL
CREATE TABLE [dbo].[Auditoria] (

     [idAuditoria] INT IDENTITY(1,1) NOT NULL
,    [TablaAfectada] VARCHAR(100) NOT NULL
,    [TipoOperacion] VARCHAR(20) NOT NULL
,    [IdRegistro] INT NOT NULL
,    [Campo] VARCHAR(100) NULL
,    [ValorAnterior] VARCHAR(MAX) NULL
,    [ValorNuevo] VARCHAR(MAX) NULL
,    [Usuario] VARCHAR(100) NOT NULL
,    [FechaHora] DATETIME NOT NULL CONSTRAINT [DF__Auditoria__Fecha__2B0A656D] DEFAULT (getdate())
,    [Estacion] VARCHAR(100) NULL
,    [IP] VARCHAR(50) NULL
,    CONSTRAINT [PK__Auditori__F1F3070176282D42] PRIMARY KEY CLUSTERED ([idAuditoria] ASC)
);
GO

IF OBJECT_ID('dbo.AuditoriaLog', 'U') IS NULL
CREATE TABLE [dbo].[AuditoriaLog] (

     [IdAuditoria] INT IDENTITY(1,1) NOT NULL
,    [FechaHora] DATETIME NOT NULL CONSTRAINT [DF__Auditoria__Fecha__0BB1B5A5] DEFAULT (getdate())
,    [IdUsuario] INT NULL
,    [NombreUsuario] NVARCHAR(200) NOT NULL
,    [RolUsuario] NVARCHAR(100) NULL
,    [Accion] NVARCHAR(50) NOT NULL
,    [TablaAfectada] NVARCHAR(100) NOT NULL
,    [IdRegistroAfectado] NVARCHAR(50) NULL
,    [Descripcion] NVARCHAR(MAX) NULL
,    [ValoresAnteriores] NVARCHAR(MAX) NULL
,    [ValoresNuevos] NVARCHAR(MAX) NULL
,    [DireccionIP] NVARCHAR(50) NULL
,    [Navegador] NVARCHAR(500) NULL
,    CONSTRAINT [PK__Auditori__7FD13FA07EA5F6C3] PRIMARY KEY CLUSTERED ([IdAuditoria] ASC)
);
GO

IF OBJECT_ID('dbo.camionetas', 'U') IS NULL
CREATE TABLE [dbo].[camionetas] (

     [id] INT NOT NULL
,    [placa] VARCHAR(15) NOT NULL
,    [tipo] VARCHAR(40) NULL
,    [marca] VARCHAR(40) NULL
,    [modelo] VARCHAR(60) NULL
,    [anio] INT NULL
,    [motor] VARCHAR(30) NULL
,    [serie_chasis] VARCHAR(30) NULL
,    CONSTRAINT [PK__camionet__3213E83FCC9C55C8] PRIMARY KEY CLUSTERED ([id] ASC)
);
GO

IF OBJECT_ID('dbo.Carreta', 'U') IS NULL
CREATE TABLE [dbo].[Carreta] (

     [idCarreta] INT IDENTITY(1,1) NOT NULL
,    [placaCarreta] VARCHAR(10) NULL
,    [modelo] VARCHAR(30) NULL
,    [marca] VARCHAR(30) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Carreta__activo__0D99FE17] DEFAULT ((1))
,    CONSTRAINT [PK__Carreta__7B19B0D44B50C458] PRIMARY KEY CLUSTERED ([idCarreta] ASC)
);
GO

IF OBJECT_ID('dbo.CategoriasAdicionales', 'U') IS NULL
CREATE TABLE [dbo].[CategoriasAdicionales] (

     [idCategoriaAdicional] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [nombreCategoria] VARCHAR(50) NULL
,    [soles] FLOAT NULL
,    [dolares] FLOAT NULL
,    [descripcion] VARCHAR(50) NULL
,    CONSTRAINT [PK__Categori__48AF3AD34D5FB732] PRIMARY KEY CLUSTERED ([idCategoriaAdicional] ASC)
);
GO

IF OBJECT_ID('dbo.Cliente', 'U') IS NULL
CREATE TABLE [dbo].[Cliente] (

     [idCliente] INT IDENTITY(1,1) NOT NULL
,    [ruc] CHAR(11) NULL
,    [nombre] VARCHAR(100) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Cliente__activo__0F824689] DEFAULT ((1))
,    CONSTRAINT [PK__Cliente__885457EE35BBD395] PRIMARY KEY CLUSTERED ([idCliente] ASC)
);
GO

IF OBJECT_ID('dbo.ClientesObra', 'U') IS NULL
CREATE TABLE [dbo].[ClientesObra] (

     [idClienteObra] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(200) NOT NULL
,    [ruc] VARCHAR(20) NULL
,    [contacto] VARCHAR(200) NULL
,    [telefono] VARCHAR(20) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__ClientesO__activ__505BE5AD] DEFAULT ((1))
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__ClientesO__fecha__515009E6] DEFAULT (getdate())
,    CONSTRAINT [PK__Clientes__375CDD0BC0F6BF03] PRIMARY KEY CLUSTERED ([idClienteObra] ASC)
);
GO

IF OBJECT_ID('dbo.Conductor', 'U') IS NULL
CREATE TABLE [dbo].[Conductor] (

     [idConductor] INT IDENTITY(1,1) NOT NULL
,    [DNI] CHAR(8) NULL
,    [nombre] VARCHAR(50) NULL
,    [apPaterno] VARCHAR(20) NULL
,    [apMaterno] VARCHAR(20) NULL
,    [fechaNacimiento] DATE NULL
,    [direccion] VARCHAR(250) NULL
,    [telefono] VARCHAR(12) NULL
,    [correo] VARCHAR(100) NULL
,    [carnetExtranjeria] CHAR(11) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Conductor__activ__0E8E2250] DEFAULT ((1))
,    CONSTRAINT [PK__Conducto__2E74F8E871DFFCDD] PRIMARY KEY CLUSTERED ([idConductor] ASC)
);
GO

IF OBJECT_ID('dbo.CPIC', 'U') IS NULL
CREATE TABLE [dbo].[CPIC] (

     [idCPIC] INT IDENTITY(1,1) NOT NULL
,    [numeroCPIC] VARCHAR(50) NOT NULL
,    [idFactura] INT NULL
,    [valorTotalFlete] DECIMAL(18,2) NOT NULL
,    [fechaEmision] DATE NOT NULL CONSTRAINT [DF__CPIC__fechaEmisi__2BFE89A6] DEFAULT (getdate())
,    [pesoNeto] DECIMAL(10,2) NULL CONSTRAINT [DF_CPIC_PesoNeto] DEFAULT ((0))
,    [pesoBruto] DECIMAL(10,2) NULL CONSTRAINT [DF_CPIC_PesoBruto] DEFAULT ((0))
,    CONSTRAINT [PK__CPIC__07F5DDBFB9FC35F7] PRIMARY KEY CLUSTERED ([idCPIC] ASC)
);
GO

IF OBJECT_ID('dbo.CPIC_Productos', 'U') IS NULL
CREATE TABLE [dbo].[CPIC_Productos] (

     [idCPIC] INT NULL
,    [idProducto] INT NULL
,    [cantidadBolsasProducto] INT NOT NULL CONSTRAINT [DF_CantidadBolsas] DEFAULT ((1))
,    [pesoKg] DECIMAL(10,2) NOT NULL CONSTRAINT [DF__CPIC_Prod__pesoK__2EDAF651] DEFAULT ((0))
);
GO

IF OBJECT_ID('dbo.Departamento', 'U') IS NULL
CREATE TABLE [dbo].[Departamento] (

     [idDepartamento] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(20) NULL
,    [idPais] INT NULL
,    CONSTRAINT [PK__Departam__C225F98DF814EAE9] PRIMARY KEY CLUSTERED ([idDepartamento] ASC)
);
GO

IF OBJECT_ID('dbo.DescuentosReintegros', 'U') IS NULL
CREATE TABLE [dbo].[DescuentosReintegros] (

     [idDescuentoReintegro] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [descuentoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__Descuento__descu__1B29035F] DEFAULT ((0))
,    [descuentoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__Descuento__descu__1C1D2798] DEFAULT ((0))
,    [reintegroSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__Descuento__reint__1D114BD1] DEFAULT ((0))
,    [reintegroDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__Descuento__reint__1E05700A] DEFAULT ((0))
,    [observacionesDescuento] VARCHAR(250) NULL
,    [observacionesReintegro] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Descuento__fecha__1EF99443] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__Descuento__activ__1FEDB87C] DEFAULT ((1))
,    CONSTRAINT [PK_DescuentosReintegros] PRIMARY KEY CLUSTERED ([idDescuentoReintegro] ASC)
);
GO

IF OBJECT_ID('dbo.DespachoCombustibleObra', 'U') IS NULL
CREATE TABLE [dbo].[DespachoCombustibleObra] (

     [idDespachoObra] INT IDENTITY(1,1) NOT NULL
,    [numeroDespacho] CHAR(6) NOT NULL
,    [idTracto] INT NOT NULL
,    [idConductor] INT NOT NULL
,    [idObra] INT NOT NULL
,    [idAbastecimientoOrigen] INT NULL
,    [fechaSalidaGrifo] DATETIME NOT NULL
,    [fechaLlegadaObra] DATETIME NULL
,    [fechaRetornoGrifo] DATETIME NULL
,    [galonesSalida] DECIMAL(12,2) NOT NULL CONSTRAINT [DF__DespachoC__galon__290D0E62] DEFAULT ((0))
,    [galonesRetorno] DECIMAL(12,2) NOT NULL CONSTRAINT [DF__DespachoC__galon__2A01329B] DEFAULT ((0))
,    [galonesAbastecidos] DECIMAL(12,2) NOT NULL CONSTRAINT [DF__DespachoC__galon__2AF556D4] DEFAULT ((0))
,    [observaciones] VARCHAR(500) NULL
,    [usuarioRegistro] VARCHAR(100) NULL
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__DespachoC__fecha__2BE97B0D] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DespachoC__activ__2CDD9F46] DEFAULT ((1))
,    CONSTRAINT [PK__Despacho__E8DA9C4B99233659] PRIMARY KEY CLUSTERED ([idDespachoObra] ASC)
);
GO

IF OBJECT_ID('dbo.Despachos', 'U') IS NULL
CREATE TABLE [dbo].[Despachos] (

     [idDespacho] INT IDENTITY(1,1) NOT NULL
,    [numeroDespacho] VARCHAR(50) NOT NULL
,    [fechaDespacho] DATE NOT NULL
,    [horaDespacho] TIME(7) NULL
,    [idConductor] INT NOT NULL
,    [idTracto] INT NOT NULL
,    [idCarreta] INT NOT NULL
,    [idCliente] INT NOT NULL
,    [idProducto] INT NULL
,    [lugarOperacion] VARCHAR(100) NOT NULL
,    [tipoOperacion] VARCHAR(50) NOT NULL
,    [estadoDespacho] VARCHAR(20) NOT NULL CONSTRAINT [DF__Despachos__estad__0169315C] DEFAULT ('PROGRAMADO')
,    [observaciones] VARCHAR(500) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Despachos__fecha__025D5595] DEFAULT (getdate())
,    [usuarioCreacion] VARCHAR(50) NULL
,    [fechaModificacion] DATETIME NULL
,    [usuarioModificacion] VARCHAR(50) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Despachos__activ__035179CE] DEFAULT ((1))
,    [idOrdenViaje] INT NULL
,    [numeroPedido] VARCHAR(10) NULL
,    [idFactura] INT NULL
,    [idCPIC] INT NULL
,    [guiaRemitente] VARCHAR(50) NULL
,    [guiaTransportista] VARCHAR(50) NULL
,    [esInternacional] BIT NULL CONSTRAINT [DF__Despachos__esInt__30242045] DEFAULT ((0))
,    [idViajeProgreso] INT NULL
,    CONSTRAINT [PK_Despachos] PRIMARY KEY CLUSTERED ([idDespacho] ASC)
,    CONSTRAINT [UK_NumeroDespacho] UNIQUE NONCLUSTERED ([numeroDespacho] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleAlimentacion', 'U') IS NULL
CREATE TABLE [dbo].[DetalleAlimentacion] (

     [idDetalleAlimentacion] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleAl__monto__3CBF0154] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleAl__monto__3DB3258D] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleAl__fecha__3EA749C6] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleAl__activ__3F9B6DFF] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleAlimentacion] PRIMARY KEY CLUSTERED ([idDetalleAlimentacion] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleApoyoSeguridad', 'U') IS NULL
CREATE TABLE [dbo].[DetalleApoyoSeguridad] (

     [idDetalleApoyoSeguridad] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleAp__monto__4460231C] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleAp__monto__45544755] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleAp__fecha__46486B8E] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleAp__activ__473C8FC7] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleApoyoSeguridad] PRIMARY KEY CLUSTERED ([idDetalleApoyoSeguridad] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleCombustible', 'U') IS NULL
CREATE TABLE [dbo].[DetalleCombustible] (

     [idDetalleCombustible] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleCo__monto__6A85CC04] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleCo__monto__6B79F03D] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleCo__fecha__6C6E1476] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleCo__activ__6D6238AF] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleCombustible] PRIMARY KEY CLUSTERED ([idDetalleCombustible] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleEncapada', 'U') IS NULL
CREATE TABLE [dbo].[DetalleEncapada] (

     [idDetalleEncapada] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleEn__monto__5B438874] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleEn__monto__5C37ACAD] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleEn__fecha__5D2BD0E6] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleEn__activ__5E1FF51F] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleEncapada] PRIMARY KEY CLUSTERED ([idDetalleEncapada] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleHospedaje', 'U') IS NULL
CREATE TABLE [dbo].[DetalleHospedaje] (

     [idDetalleHospedaje] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleHo__monto__62E4AA3C] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleHo__monto__63D8CE75] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleHo__fecha__64CCF2AE] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleHo__activ__65C116E7] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleHospedaje] PRIMARY KEY CLUSTERED ([idDetalleHospedaje] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleMovilidad', 'U') IS NULL
CREATE TABLE [dbo].[DetalleMovilidad] (

     [idDetalleMovilidad] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleMo__monto__53A266AC] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleMo__monto__54968AE5] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleMo__fecha__558AAF1E] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleMo__activ__567ED357] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleMovilidad] PRIMARY KEY CLUSTERED ([idDetalleMovilidad] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleOrdenViaje', 'U') IS NULL
CREATE TABLE [dbo].[DetalleOrdenViaje] (

     [idDetalleOrdenViaje] INT IDENTITY(1,1) NOT NULL
,    [idGuia] INT NULL
,    [idProducto] INT NULL
,    [cantidadBolsas] INT NULL
,    CONSTRAINT [PK__DetalleO__E0DF288897757982] PRIMARY KEY CLUSTERED ([idDetalleOrdenViaje] ASC)
);
GO

IF OBJECT_ID('dbo.DetallePeajes', 'U') IS NULL
CREATE TABLE [dbo].[DetallePeajes] (

     [idDetallePeaje] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [estacion] VARCHAR(100) NOT NULL
,    [fecha] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetallePe__monto__351DDF8C] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetallePe__monto__361203C5] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetallePe__fecha__370627FE] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetallePe__activ__37FA4C37] DEFAULT ((1))
,    CONSTRAINT [PK_DetallePeajes] PRIMARY KEY CLUSTERED ([idDetallePeaje] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleReparacionesVarios', 'U') IS NULL
CREATE TABLE [dbo].[DetalleReparacionesVarios] (

     [idDetalleReparacionesVarios] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NOT NULL
,    [fechaComprobante] DATE NOT NULL
,    [numeroComprobante] VARCHAR(50) NULL
,    [montoSoles] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleRe__monto__4C0144E4] DEFAULT ((0))
,    [montoDolares] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleRe__monto__4CF5691D] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__DetalleRe__fecha__4DE98D56] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__DetalleRe__activ__4EDDB18F] DEFAULT ((1))
,    CONSTRAINT [PK_DetalleReparacionesVarios] PRIMARY KEY CLUSTERED ([idDetalleReparacionesVarios] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleSegmento', 'U') IS NULL
CREATE TABLE [dbo].[DetalleSegmento] (

     [idDetalleSegmento] INT IDENTITY(1,1) NOT NULL
,    [idSegmento] INT NOT NULL
,    [idProducto] INT NOT NULL
,    [cantidadBolsas] INT NOT NULL CONSTRAINT [DF__DetalleSe__canti__7E02B4CC] DEFAULT ((1))
,    [pesoKg] DECIMAL(10,2) NULL CONSTRAINT [DF__DetalleSe__pesoK__7EF6D905] DEFAULT ((0))
,    [observacionesProducto] VARCHAR(250) NULL
,    CONSTRAINT [PK_DetalleSegmento] PRIMARY KEY CLUSTERED ([idDetalleSegmento] ASC)
);
GO

IF OBJECT_ID('dbo.DetalleTicketEcuador', 'U') IS NULL
CREATE TABLE [dbo].[DetalleTicketEcuador] (

     [idDetalle] INT IDENTITY(1,1) NOT NULL
,    [idIngreso] INT NOT NULL
,    [numeroTicket] VARCHAR(50) NULL
,    [proveedor] VARCHAR(150) NULL
,    [galones] DECIMAL(11,2) NOT NULL CONSTRAINT [DF__DetalleTi__galon__01F34141] DEFAULT ((0))
,    [precioUSD] DECIMAL(11,2) NOT NULL CONSTRAINT [DF__DetalleTi__preci__02E7657A] DEFAULT ((0))
,    CONSTRAINT [PK__DetalleT__49CAE2FB30BBFF28] PRIMARY KEY CLUSTERED ([idDetalle] ASC)
);
GO

IF OBJECT_ID('dbo.Distrito', 'U') IS NULL
CREATE TABLE [dbo].[Distrito] (

     [idDistrito] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(20) NULL
,    [idProvincia] INT NULL
,    CONSTRAINT [PK__Distrito__494092A88C36F4D8] PRIMARY KEY CLUSTERED ([idDistrito] ASC)
);
GO

IF OBJECT_ID('dbo.DocumentosCPIC', 'U') IS NULL
CREATE TABLE [dbo].[DocumentosCPIC] (

     [idDocumento] INT IDENTITY(1,1) NOT NULL
,    [idCPIC] INT NOT NULL
,    [nombreOriginal] VARCHAR(255) NOT NULL
,    [nombreArchivo] VARCHAR(255) NOT NULL
,    [rutaArchivo] VARCHAR(500) NOT NULL
,    [tipoArchivo] VARCHAR(10) NOT NULL
,    [tamanoBytes] BIGINT NOT NULL
,    [fechaSubida] DATETIME NOT NULL CONSTRAINT [DF__Documento__fecha__30C33EC3] DEFAULT (getdate())
,    [usuarioSubida] VARCHAR(50) NULL
,    [descripcion] VARCHAR(300) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Documento__activ__31B762FC] DEFAULT ((1))
,    CONSTRAINT [PK__Document__572A36FCFA6F60F0] PRIMARY KEY CLUSTERED ([idDocumento] ASC)
);
GO

IF OBJECT_ID('dbo.DocumentosFactura', 'U') IS NULL
CREATE TABLE [dbo].[DocumentosFactura] (

     [idDocumento] INT IDENTITY(1,1) NOT NULL
,    [idFactura] INT NOT NULL
,    [nombreOriginal] VARCHAR(255) NOT NULL
,    [nombreArchivo] VARCHAR(255) NOT NULL
,    [rutaArchivo] VARCHAR(500) NOT NULL
,    [tipoArchivo] VARCHAR(10) NOT NULL
,    [tamanoBytes] BIGINT NOT NULL
,    [fechaSubida] DATETIME NOT NULL CONSTRAINT [DF__Documento__fecha__32AB8735] DEFAULT (getdate())
,    [usuarioSubida] VARCHAR(50) NULL
,    [descripcion] VARCHAR(300) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Documento__activ__339FAB6E] DEFAULT ((1))
,    CONSTRAINT [PK__Document__572A36FC73DCA5F1] PRIMARY KEY CLUSTERED ([idDocumento] ASC)
);
GO

IF OBJECT_ID('dbo.Egresos', 'U') IS NULL
CREATE TABLE [dbo].[Egresos] (

     [idEgresos] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [peajesDolares] FLOAT NULL
,    [peajesSoles] FLOAT NULL
,    [descPeajes] VARCHAR(1000) NULL
,    [alimentacionSoles] FLOAT NULL
,    [alimentacionDolares] FLOAT NULL
,    [descAlimentacion] VARCHAR(500) NULL
,    [apoyoseguridadSoles] FLOAT NULL
,    [apoyoseguridadDolares] FLOAT NULL
,    [descApoyoSeguridad] VARCHAR(500) NULL
,    [reparacionesVariosSoles] FLOAT NULL
,    [repacionesVariosDolares] FLOAT NULL
,    [descReparacionesVarios] VARCHAR(500) NULL
,    [movilidadSoles] FLOAT NULL
,    [movilidadDolares] FLOAT NULL
,    [descMovilidad] VARCHAR(500) NULL
,    [hospedajeSoles] FLOAT NULL
,    [hospedajeDolares] FLOAT NULL
,    [descHospedaje] VARCHAR(500) NULL
,    [combustibleSoles] FLOAT NULL
,    [combustibleDolares] FLOAT NULL
,    [descCombustible] VARCHAR(500) NULL
,    [encarpada_desencarpadaSoles] FLOAT NULL
,    [encarpada_desencarpadaDolares] FLOAT NULL
,    [descEncarpadaDesencarpada] VARCHAR(500) NULL
,    CONSTRAINT [PK__Egresos__C508E28A8B177931] PRIMARY KEY CLUSTERED ([idEgresos] ASC)
);
GO

IF OBJECT_ID('dbo.EquiposMaquinaria', 'U') IS NULL
CREATE TABLE [dbo].[EquiposMaquinaria] (

     [idEquipo] INT IDENTITY(1,1) NOT NULL
,    [placa] VARCHAR(20) NOT NULL
,    [descripcion] VARCHAR(200) NULL
,    [tipo] VARCHAR(50) NULL
,    [marca] VARCHAR(100) NULL
,    [modelo] VARCHAR(100) NULL
,    [anio] INT NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__EquiposMa__activ__4C8B54C9] DEFAULT ((1))
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__EquiposMa__fecha__4D7F7902] DEFAULT (getdate())
,    CONSTRAINT [PK__EquiposM__981ACF530621CF11] PRIMARY KEY CLUSTERED ([idEquipo] ASC)
,    CONSTRAINT [UQ__EquiposM__0C057425B1CF3A5F] UNIQUE NONCLUSTERED ([placa] ASC)
);
GO

IF OBJECT_ID('dbo.EstacionesPeaje', 'U') IS NULL
CREATE TABLE [dbo].[EstacionesPeaje] (

     [idEstacion] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(100) NOT NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Estacione__activ__0FB750B3] DEFAULT ((1))
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Estacione__fecha__10AB74EC] DEFAULT (getdate())
,    CONSTRAINT [PK_EstacionesPeaje] PRIMARY KEY CLUSTERED ([idEstacion] ASC)
,    CONSTRAINT [UK_EstacionesPeaje_Nombre] UNIQUE NONCLUSTERED ([nombre] ASC)
);
GO

IF OBJECT_ID('dbo.Factura', 'U') IS NULL
CREATE TABLE [dbo].[Factura] (

     [idFactura] INT IDENTITY(1,1) NOT NULL
,    [numeroFactura] VARCHAR(50) NOT NULL
,    [valorTotal] DECIMAL(18,2) NOT NULL
,    [fechaEmision] DATE NOT NULL
,    [numeroPedido] VARCHAR(10) NULL
,    [idCliente] INT NOT NULL CONSTRAINT [DF__Factura__idClien__7908F585] DEFAULT ((1))
,    CONSTRAINT [PK__Factura__3CD5687E9BBADB26] PRIMARY KEY CLUSTERED ([idFactura] ASC)
);
GO

IF OBJECT_ID('dbo.FirmaDigital', 'U') IS NULL
CREATE TABLE [dbo].[FirmaDigital] (

     [idFirma] INT IDENTITY(1,1) NOT NULL
,    [tipoDocumento] VARCHAR(40) NOT NULL
,    [idDocumento] VARCHAR(50) NOT NULL
,    [nivelFirma] CHAR(1) NOT NULL
,    [idUsuarioFirmante] INT NULL
,    [dniFirmante] VARCHAR(15) NULL
,    [nombreFirmante] VARCHAR(150) NOT NULL
,    [rolFirmante] VARCHAR(30) NOT NULL
,    [imagenTrazoPng] VARBINARY(MAX) NULL
,    [hashDocumento] CHAR(64) NOT NULL
,    [textoConsentimiento] VARCHAR(1000) NOT NULL
,    [fechaHoraFirma] DATETIME2(0) NOT NULL CONSTRAINT [DF__FirmaDigi__fecha__3C1FE2D6] DEFAULT (sysutcdatetime())
,    [ipOrigen] VARCHAR(45) NULL
,    [userAgent] VARCHAR(500) NULL
,    [rutaPdfFirmado] VARCHAR(500) NULL
,    [estadoFirma] CHAR(1) NOT NULL CONSTRAINT [DF__FirmaDigi__estad__3D14070F] DEFAULT ('V')
,    [idFirmaAnulada] INT NULL
,    [motivoAnulacion] VARCHAR(500) NULL
,    CONSTRAINT [PK__FirmaDig__A9CB15C28E4D7270] PRIMARY KEY CLUSTERED ([idFirma] ASC)
);
GO

IF OBJECT_ID('dbo.FormatoControlado', 'U') IS NULL
CREATE TABLE [dbo].[FormatoControlado] (

     [idFormato] INT IDENTITY(1,1) NOT NULL
,    [codigoFormato] VARCHAR(30) NOT NULL
,    [nombre] VARCHAR(150) NOT NULL
,    [version] VARCHAR(5) NOT NULL
,    [fechaVigencia] DATE NOT NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__FormatoCo__activ__384F51F2] DEFAULT ((1))
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__FormatoCo__fecha__3943762B] DEFAULT (getdate())
,    CONSTRAINT [PK__FormatoC__60E489D171C09AE9] PRIMARY KEY CLUSTERED ([idFormato] ASC)
,    CONSTRAINT [UQ__FormatoC__4C1BBEB0729E5A40] UNIQUE NONCLUSTERED ([codigoFormato] ASC)
);
GO

IF OBJECT_ID('dbo.GuiasTransportista', 'U') IS NULL
CREATE TABLE [dbo].[GuiasTransportista] (

     [idGuia] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [numeroGuiaTransportista] VARCHAR(50) NULL
,    [numeroGuiaCliente] VARCHAR(50) NULL
,    [ruta1] VARCHAR(50) NULL
,    [ruta2] VARCHAR(50) NULL
,    [numeroManifiesto] VARCHAR(50) NULL
,    [plantaDescarga] VARCHAR(50) NULL
,    [descripcionProducto] VARCHAR(250) NULL
,    CONSTRAINT [PK__GuiasTra__9C66717368032C51] PRIMARY KEY CLUSTERED ([idGuia] ASC)
);
GO

IF OBJECT_ID('dbo.Indicadores', 'U') IS NULL
CREATE TABLE [dbo].[Indicadores] (

     [idIndicador] INT IDENTITY(1,1) NOT NULL
,    [numeroPedido] VARCHAR(20) NOT NULL
,    [conductorOrigen] VARCHAR(100) NULL
,    [tracto1] VARCHAR(20) NULL
,    [carreta] VARCHAR(20) NULL
,    [conductorDestino] VARCHAR(100) NULL
,    [tracto2] VARCHAR(20) NULL
,    [fechaHoraSalidaBase] DATETIME NULL
,    [fechaHoraLlegadaTrujillo] DATETIME NULL
,    [fechaHoraRegistro] DATETIME NULL
,    [fechaHoraProgramacion] DATETIME NULL
,    [fechaHoraIngresoPlanta] DATETIME NULL
,    [fechaHoraInicioCarga] DATETIME NULL
,    [fechaHoraTerminoCarga] DATETIME NULL
,    [fechaHoraSalidaPlanta] DATETIME NULL
,    [fechaHoraLlegadaBase] DATETIME NULL
,    [fechaHoraSalidaBaseDepsa] DATETIME NULL
,    [fechaHoraLlegadaDepsa] DATETIME NULL
,    [fechaHoraInicioDepsa] DATETIME NULL
,    [fechaHoraSalidaDepsa] DATETIME NULL
,    [bodega] VARCHAR(100) NULL
,    [fechaHoraLlegadaCebafE] DATETIME NULL
,    [fechaHoraCruceE] DATETIME NULL
,    [fechaHoraAutorizacionNacionalizacion] DATETIME NULL
,    [bodegaEcuatoriana] VARCHAR(100) NULL
,    [fechaHoraLlegadaTCI] DATETIME NULL
,    [fechaHoraSalidaTCI] DATETIME NULL
,    [bodegaDescarga] VARCHAR(100) NULL
,    [fechaHoraLlegadaPlantaDescarga] DATETIME NULL
,    [fechaHoraLlegadaAlmacen] DATETIME NULL
,    [fechaHoraIngreso] DATETIME NULL
,    [fechaHoraInicioDescarga] DATETIME NULL
,    [fechaHoraTerminoDescarga] DATETIME NULL
,    [fechaHoraSalida] DATETIME NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Indicador__fecha__3587F3E0] DEFAULT (getdate())
,    [usuarioCreacion] VARCHAR(50) NULL
,    [UploadID] INT NOT NULL CONSTRAINT [DF__Indicador__Uploa__3493CFA7] DEFAULT ((0))
,    CONSTRAINT [PK__Indicado__1F4DEAEF3F3F9FE3] PRIMARY KEY CLUSTERED ([idIndicador] ASC)
);
GO

IF OBJECT_ID('dbo.IngresoCombustibleEcuador', 'U') IS NULL
CREATE TABLE [dbo].[IngresoCombustibleEcuador] (

     [idIngreso] INT IDENTITY(1,1) NOT NULL
,    [idViajeProgreso] INT NULL
,    [idConductor] INT NULL
,    [idTracto] INT NULL
,    [fechaRecepcion] DATETIME NOT NULL CONSTRAINT [DF__IngresoCo__fecha__795DFB40] DEFAULT (getdate())
,    [totalGalones] DECIMAL(11,2) NOT NULL CONSTRAINT [DF__IngresoCo__total__7A521F79] DEFAULT ((0))
,    [totalUSD] DECIMAL(11,2) NOT NULL CONSTRAINT [DF__IngresoCo__total__7B4643B2] DEFAULT ((0))
,    [observaciones] VARCHAR(500) NULL
,    [usuarioRegistro] VARCHAR(100) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__IngresoCo__activ__7C3A67EB] DEFAULT ((1))
,    CONSTRAINT [PK__IngresoC__5E6E52C409A5BD65] PRIMARY KEY CLUSTERED ([idIngreso] ASC)
);
GO

IF OBJECT_ID('dbo.Ingresos', 'U') IS NULL
CREATE TABLE [dbo].[Ingresos] (

     [idIngreso] INT IDENTITY(1,1) NOT NULL
,    [despachoSoles] FLOAT NULL
,    [despachoDolares] FLOAT NULL
,    [prestamoSoles] FLOAT NULL
,    [prestamosDolares] FLOAT NULL
,    [mensualidadSoles] FLOAT NULL
,    [mensualidadDolares] FLOAT NULL
,    [otrosSoles] FLOAT NULL
,    [otrosDolares] FLOAT NULL
,    [totalDolares] FLOAT NULL
,    [totalSoles] FLOAT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [descDespacho] VARCHAR(250) NULL
,    [descMensualidad] VARCHAR(250) NULL
,    [descOtrosAutorizados] VARCHAR(250) NULL
,    [descPrestamo] VARCHAR(250) NULL
,    CONSTRAINT [PK__Ingresos__5E6E52C47FE6845C] PRIMARY KEY CLUSTERED ([idIngreso] ASC)
);
GO

IF OBJECT_ID('dbo.IngresosAdicionales', 'U') IS NULL
CREATE TABLE [dbo].[IngresosAdicionales] (

     [idIngresoAdicional] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [nombreCategoria] VARCHAR(50) NULL
,    [soles] FLOAT NULL
,    [dolares] FLOAT NULL
,    [descripcion] VARCHAR(250) NULL
,    CONSTRAINT [PK__Ingresos__4B4FCC6FF3B5C07F] PRIMARY KEY CLUSTERED ([idIngresoAdicional] ASC)
);
GO

IF OBJECT_ID('dbo.Liquidaciones', 'U') IS NULL
CREATE TABLE [dbo].[Liquidaciones] (

     [idLiquidacion] INT IDENTITY(1,1) NOT NULL
,    [idOrdenViaje] INT NOT NULL
,    [numeroLiquidacion] INT NOT NULL
,    [tipo] VARCHAR(20) NOT NULL
,    [descripcion] VARCHAR(200) NULL
,    [observaciones] VARCHAR(500) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Liquidaci__fecha__42ACE4D4] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__Liquidaci__activ__43A1090D] DEFAULT ((1))
,    CONSTRAINT [PK_Liquidaciones] PRIMARY KEY CLUSTERED ([idLiquidacion] ASC)
,    CONSTRAINT [UK_Liquidacion_Numero] UNIQUE NONCLUSTERED ([idOrdenViaje] ASC, [numeroLiquidacion] ASC)
);
GO

IF OBJECT_ID('dbo.LugarAbastecimiento', 'U') IS NULL
CREATE TABLE [dbo].[LugarAbastecimiento] (

     [idLugarAbastecimiento] INT IDENTITY(1,1) NOT NULL
,    [nombreAbastecimiento] VARCHAR(100) NOT NULL
,    CONSTRAINT [PK__LugarAba__B528F8E8B4EF4AB1] PRIMARY KEY CLUSTERED ([idLugarAbastecimiento] ASC)
,    CONSTRAINT [UQ__LugarAba__8D5A2D424F696196] UNIQUE NONCLUSTERED ([nombreAbastecimiento] ASC)
);
GO

IF OBJECT_ID('dbo.Lugares', 'U') IS NULL
CREATE TABLE [dbo].[Lugares] (

     [idLugar] INT IDENTITY(1,1) NOT NULL
,    [nombre] NVARCHAR(100) NOT NULL
,    [codigo] NVARCHAR(20) NOT NULL
,    [activo] BIT NULL CONSTRAINT [DF__Lugares__activo__2F2FFC0C] DEFAULT ((1))
,    CONSTRAINT [PK__Lugares__F7460D5FF5BAE5DF] PRIMARY KEY CLUSTERED ([idLugar] ASC)
);
GO

IF OBJECT_ID('dbo.Manifiesto', 'U') IS NULL
CREATE TABLE [dbo].[Manifiesto] (

     [idManifiesto] INT IDENTITY(1,1) NOT NULL
,    [numeroManifiesto] VARCHAR(50) NOT NULL
,    [idGuia] INT NULL
,    [idCPIC] INT NULL
,    CONSTRAINT [PK__Manifies__B63B10678819FBB3] PRIMARY KEY CLUSTERED ([idManifiesto] ASC)
,    CONSTRAINT [UC_NumeroManifiesto] UNIQUE NONCLUSTERED ([numeroManifiesto] ASC)
);
GO

IF OBJECT_ID('dbo.Obras', 'U') IS NULL
CREATE TABLE [dbo].[Obras] (

     [idObra] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(300) NOT NULL
,    [idClienteObra] INT NOT NULL
,    [ubicacion] VARCHAR(300) NULL
,    [estado] VARCHAR(20) NOT NULL CONSTRAINT [DF__Obras__estado__542C7691] DEFAULT ('ACTIVA')
,    [fechaInicio] DATE NULL
,    [fechaFin] DATE NULL
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__Obras__fechaRegi__55209ACA] DEFAULT (getdate())
,    CONSTRAINT [PK__Obras__D4C68C4382F1CDFD] PRIMARY KEY CLUSTERED ([idObra] ASC)
);
GO

IF OBJECT_ID('dbo.OperacionesSubTramo', 'U') IS NULL
CREATE TABLE [dbo].[OperacionesSubTramo] (

     [idOperacion] INT IDENTITY(1,1) NOT NULL
,    [idSubTramo] INT NOT NULL
,    [tipoOperacion] VARCHAR(20) NOT NULL
,    [idCliente] INT NOT NULL
,    [idFactura] INT NULL
,    [idCPIC] INT NULL
,    [esInternacional] BIT NOT NULL CONSTRAINT [DF__Operacion__esInt__52E34C9D] DEFAULT ((0))
,    [observaciones] VARCHAR(300) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Operacion__fecha__53D770D6] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__Operacion__activ__54CB950F] DEFAULT ((1))
,    [idPlantaCarga] INT NULL
,    [idPlantaDescarga] INT NULL
,    CONSTRAINT [PK_OperacionesSubTramo] PRIMARY KEY CLUSTERED ([idOperacion] ASC)
,    CONSTRAINT [UK_Operacion_SubTramo_Tipo] UNIQUE NONCLUSTERED ([idSubTramo] ASC, [tipoOperacion] ASC)
);
GO

IF OBJECT_ID('dbo.Operadores', 'U') IS NULL
CREATE TABLE [dbo].[Operadores] (

     [idOperador] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(200) NOT NULL
,    [dni] VARCHAR(15) NOT NULL
,    [telefono] VARCHAR(20) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Operadore__activ__59E54FE7] DEFAULT ((1))
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__Operadore__fecha__5AD97420] DEFAULT (getdate())
,    CONSTRAINT [PK__Operador__D9DC4D4EE9366EFB] PRIMARY KEY CLUSTERED ([idOperador] ASC)
,    CONSTRAINT [UQ__Operador__D87608A70CDA616A] UNIQUE NONCLUSTERED ([dni] ASC)
);
GO

IF OBJECT_ID('dbo.OrdenViaje', 'U') IS NULL
CREATE TABLE [dbo].[OrdenViaje] (

     [idOrdenViaje] INT IDENTITY(1,1) NOT NULL
,    [numeroOrdenViaje] VARCHAR(50) NULL
,    [idCliente] INT NULL
,    [idConductor] INT NULL
,    [idTracto] INT NULL
,    [idCarreta] INT NULL
,    [idProducto] INT NULL
,    [fechaSalida] DATE NULL
,    [horaSalida] TIME(7) NULL
,    [fechaLlegada] DATE NULL
,    [horaLlegada] TIME(7) NULL
,    [observaciones] VARCHAR(250) NULL
,    [idCPIC] INT NULL
,    [observacionesLiquidacion] VARCHAR(250) NULL
,    [estadoViaje] VARCHAR(20) NULL CONSTRAINT [DF__OrdenViaj__estad__03BB8E22] DEFAULT ('PENDIENTE')
,    [tipoViaje] VARCHAR(20) NULL CONSTRAINT [DF__OrdenViaj__tipoV__04AFB25B] DEFAULT ('MIXTO')
,    [idViajeProgreso] INT NULL
,    [esInternacional] BIT NULL CONSTRAINT [DF_OrdenViaje_EsInternacional] DEFAULT ((0))
,    [registradoPor] VARCHAR(20) NULL CONSTRAINT [DF_OrdenViaje_RegistradoPor] DEFAULT ('ADMIN')
,    [idUsuarioRegistro] INT NULL
,    [estadoAprobacion] VARCHAR(20) NULL CONSTRAINT [DF_OrdenViaje_EstadoAprobacion] DEFAULT ('APROBADO')
,    [fechaRegistro] DATETIME NULL CONSTRAINT [DF_OrdenViaje_FechaRegistro] DEFAULT (getdate())
,    [fechaAprobacion] DATETIME NULL
,    [idUsuarioAprobacion] INT NULL
,    [observacionesAprobacion] VARCHAR(500) NULL
,    [observacionesRechazo] NVARCHAR(500) NULL
,    [fechaRechazo] DATETIME NULL
,    [rutaPdfFirmado] VARCHAR(500) NULL
,    [hashPdfFirmado] CHAR(64) NULL
,    [idFirmaConductor] INT NULL
,    [idFirmaAdmin] INT NULL
,    [fechaEnvioFirmado] DATETIME NULL
,    [fechaAprobacionFirmada] DATETIME NULL
,    CONSTRAINT [PK__OrdenVia__0D0929708427990A] PRIMARY KEY CLUSTERED ([idOrdenViaje] ASC)
,    CONSTRAINT [UQ__OrdenVia__99BEFB7625B24CC4] UNIQUE NONCLUSTERED ([numeroOrdenViaje] ASC)
);
GO

IF OBJECT_ID('dbo.OrdenViajeAjuste', 'U') IS NULL
CREATE TABLE [dbo].[OrdenViajeAjuste] (

     [idAjuste] INT IDENTITY(1,1) NOT NULL
,    [idOrdenViaje] INT NOT NULL
,    [tipoAjuste] VARCHAR(20) NOT NULL
,    [monedaAjuste] CHAR(3) NOT NULL
,    [monto] DECIMAL(12,2) NOT NULL
,    [motivo] VARCHAR(500) NOT NULL
,    [idUsuarioAdmin] INT NOT NULL
,    [idFirmaAdmin] INT NULL
,    [fechaAplicacion] DATETIME NOT NULL CONSTRAINT [DF__OrdenViaj__fecha__42CCE065] DEFAULT (getdate())
,    CONSTRAINT [PK__OrdenVia__5B649138828E8083] PRIMARY KEY CLUSTERED ([idAjuste] ASC)
);
GO

IF OBJECT_ID('dbo.Pais', 'U') IS NULL
CREATE TABLE [dbo].[Pais] (

     [idPais] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(20) NULL
,    CONSTRAINT [PK__Pais__BD2285E32F5E9989] PRIMARY KEY CLUSTERED ([idPais] ASC)
);
GO

IF OBJECT_ID('dbo.PartesDiariosTrabajo', 'U') IS NULL
CREATE TABLE [dbo].[PartesDiariosTrabajo] (

     [idParte] INT IDENTITY(1,1) NOT NULL
,    [numeroParte] VARCHAR(20) NOT NULL
,    [idAsignacion] INT NOT NULL
,    [idOperador] INT NOT NULL
,    [fecha] DATE NOT NULL
,    [odometroComienzo] DECIMAL(10,1) NULL
,    [odometroTermino] DECIMAL(10,1) NULL
,    [odometroKmHoras] AS (case when [odometroTermino] IS NOT NULL AND [odometroComienzo] IS NOT NULL then [odometroTermino]-[odometroComienzo]  end) PERSISTED
,    [horometroComienzo] DECIMAL(10,1) NULL
,    [horometroTermino] DECIMAL(10,1) NULL
,    [horometroHoras] AS (case when [horometroTermino] IS NOT NULL AND [horometroComienzo] IS NOT NULL then [horometroTermino]-[horometroComienzo]  end) PERSISTED
,    [consumoPetroleo] DECIMAL(10,2) NULL CONSTRAINT [DF__PartesDia__consu__664B26CC] DEFAULT ((0))
,    [consumoGasolina] DECIMAL(10,2) NULL CONSTRAINT [DF__PartesDia__consu__673F4B05] DEFAULT ((0))
,    [consumoAceite] DECIMAL(10,2) NULL CONSTRAINT [DF__PartesDia__consu__68336F3E] DEFAULT ((0))
,    [consumoGrasa] DECIMAL(10,2) NULL CONSTRAINT [DF__PartesDia__consu__69279377] DEFAULT ((0))
,    [carretera] VARCHAR(200) NULL
,    [sector] VARCHAR(200) NULL
,    [sectorKm] VARCHAR(50) NULL
,    [alKm] VARCHAR(50) NULL
,    [labor] VARCHAR(300) NULL
,    [codigo] VARCHAR(50) NULL
,    [cantidadViajes] INT NULL
,    [reclamo] VARCHAR(500) NULL
,    [observaciones] VARCHAR(500) NULL
,    [estado] VARCHAR(20) NOT NULL CONSTRAINT [DF__PartesDia__estad__6A1BB7B0] DEFAULT ('REGISTRADO')
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__PartesDia__fecha__6B0FDBE9] DEFAULT (getdate())
,    [fechaModificacion] DATETIME NULL
,    CONSTRAINT [PK__PartesDi__54A22504508C2065] PRIMARY KEY CLUSTERED ([idParte] ASC)
,    CONSTRAINT [UQ__PartesDi__AB46AB0B37087B7E] UNIQUE NONCLUSTERED ([numeroParte] ASC)
);
GO

IF OBJECT_ID('dbo.Planta', 'U') IS NULL
CREATE TABLE [dbo].[Planta] (

     [idPlanta] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(200) NOT NULL
,    [esInternacional] BIT NOT NULL CONSTRAINT [DF__Planta__esIntern__125EB334] DEFAULT ((0))
,    [activo] BIT NOT NULL CONSTRAINT [DF__Planta__activo__1352D76D] DEFAULT ((1))
,    CONSTRAINT [PK__Planta__31C3EE03C9CD93B3] PRIMARY KEY CLUSTERED ([idPlanta] ASC)
);
GO

IF OBJECT_ID('dbo.PlantaCarga', 'U') IS NULL
CREATE TABLE [dbo].[PlantaCarga] (

     [idPlantaCarga] INT IDENTITY(1,1) NOT NULL
,    [nombre] NVARCHAR(100) NOT NULL
,    [idCliente] INT NULL
,    [direccion] NVARCHAR(200) NULL
,    [activa] BIT NOT NULL CONSTRAINT [DF__PlantaCar__activ__1A69E950] DEFAULT ((1))
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__PlantaCar__fecha__1B5E0D89] DEFAULT (getdate())
,    CONSTRAINT [PK__PlantaCa__4538CE812621E024] PRIMARY KEY CLUSTERED ([idPlantaCarga] ASC)
);
GO

IF OBJECT_ID('dbo.PlantaDescarga', 'U') IS NULL
CREATE TABLE [dbo].[PlantaDescarga] (

     [idPlanta] INT IDENTITY(1,1) NOT NULL
,    [nombre] NVARCHAR(100) NOT NULL
,    [idCliente] INT NULL
,    [direccion] NVARCHAR(200) NULL
,    [activa] BIT NOT NULL CONSTRAINT [DF__PlantaDes__activ__1D4655FB] DEFAULT ((1))
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__PlantaDes__fecha__1E3A7A34] DEFAULT (getdate())
,    CONSTRAINT [PK__PlantaDe__31C3EE03189886A4] PRIMARY KEY CLUSTERED ([idPlanta] ASC)
);
GO

IF OBJECT_ID('dbo.Producto', 'U') IS NULL
CREATE TABLE [dbo].[Producto] (

     [idProducto] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(50) NULL
,    [idCliente] INT NULL
,    CONSTRAINT [PK__Producto__07F4A1321D168650] PRIMARY KEY CLUSTERED ([idProducto] ASC)
);
GO

IF OBJECT_ID('dbo.ProductosOperacion', 'U') IS NULL
CREATE TABLE [dbo].[ProductosOperacion] (

     [idProductoOperacion] INT IDENTITY(1,1) NOT NULL
,    [idOperacion] INT NOT NULL
,    [idProducto] INT NOT NULL
,    [cantidadBolsas] INT NOT NULL
,    [pesoKg] DECIMAL(10,2) NULL CONSTRAINT [DF__Productos__pesoK__5E54FF49] DEFAULT ((0))
,    [observaciones] VARCHAR(250) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Productos__fecha__5F492382] DEFAULT (getdate())
,    CONSTRAINT [PK_ProductosOperacion] PRIMARY KEY CLUSTERED ([idProductoOperacion] ASC)
,    CONSTRAINT [UK_ProductoOperacion_Unico] UNIQUE NONCLUSTERED ([idOperacion] ASC, [idProducto] ASC)
);
GO

IF OBJECT_ID('dbo.Provincia', 'U') IS NULL
CREATE TABLE [dbo].[Provincia] (

     [idProvincia] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(20) NULL
,    [idDepartamento] INT NULL
,    CONSTRAINT [PK__Provinci__5F9F113C29E01211] PRIMARY KEY CLUSTERED ([idProvincia] ASC)
);
GO

IF OBJECT_ID('dbo.Roles', 'U') IS NULL
CREATE TABLE [dbo].[Roles] (

     [idRol] INT IDENTITY(1,1) NOT NULL
,    [nombreRol] VARCHAR(60) NOT NULL
,    [descripcion] VARCHAR(255) NULL
,    [nivel] INT NOT NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Roles__activo__07E124C1] DEFAULT ((1))
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Roles__fechaCrea__08D548FA] DEFAULT (getdate())
,    CONSTRAINT [PK__Roles__3C872F762B3BC0B4] PRIMARY KEY CLUSTERED ([idRol] ASC)
,    CONSTRAINT [UQ__Roles__2787B00CE38DF686] UNIQUE NONCLUSTERED ([nombreRol] ASC)
);
GO

IF OBJECT_ID('dbo.Ruta', 'U') IS NULL
CREATE TABLE [dbo].[Ruta] (

     [idRuta] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(100) NOT NULL
,    [descripcion] VARCHAR(200) NULL
,    [idCliente] INT NULL
,    CONSTRAINT [PK__Ruta__E584E6F439F144BD] PRIMARY KEY CLUSTERED ([idRuta] ASC)
,    CONSTRAINT [UQ__Ruta__72AFBCC672230344] UNIQUE NONCLUSTERED ([nombre] ASC)
);
GO

IF OBJECT_ID('dbo.SegmentosOrdenViaje', 'U') IS NULL
CREATE TABLE [dbo].[SegmentosOrdenViaje] (

     [idSegmento] INT IDENTITY(1,1) NOT NULL
,    [idOrdenViaje] INT NOT NULL
,    [numeroSegmento] INT NOT NULL
,    [idCliente] INT NOT NULL
,    [idCPIC] INT NULL
,    [origen] VARCHAR(100) NOT NULL
,    [destino] VARCHAR(100) NOT NULL
,    [tipoOperacion] VARCHAR(20) NOT NULL
,    [esInternacional] BIT NOT NULL CONSTRAINT [DF__Segmentos__esInt__756D6ECB] DEFAULT ((0))
,    [observacionesSegmento] VARCHAR(500) NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Segmentos__fecha__76619304] DEFAULT (getdate())
,    [idFactura] INT NULL
,    [guiaTransportista] VARCHAR(50) NULL
,    [guiaCliente] VARCHAR(50) NULL
,    [cruzaFrontera] BIT NULL
,    [manifiesto] VARCHAR(50) NULL
,    CONSTRAINT [PK_SegmentosOrdenViaje] PRIMARY KEY CLUSTERED ([idSegmento] ASC)
);
GO

IF OBJECT_ID('dbo.SeguimientoExportacion', 'U') IS NULL
CREATE TABLE [dbo].[SeguimientoExportacion] (

     [idSeguimiento] INT IDENTITY(1,1) NOT NULL
,    [cliente] VARCHAR(150) NULL
,    [conductorOrigen] VARCHAR(150) NULL
,    [tracto1] VARCHAR(20) NULL
,    [carreta] VARCHAR(20) NULL
,    [conductorDestino] VARCHAR(150) NULL
,    [tracto2] VARCHAR(20) NULL
,    [fhSalidaBase1] DATETIME NULL
,    [fhLlegadaTrujillo] DATETIME NULL
,    [fhRegistro] DATETIME NULL
,    [fhProgramacion] DATETIME NULL
,    [fhIngresoPlanta] DATETIME NULL
,    [fhInicioCarga] DATETIME NULL
,    [fhTerminoCarga] DATETIME NULL
,    [fhSalidaPlanta] DATETIME NULL
,    [fhLlegadaBase2] DATETIME NULL
,    [fhSalidaBase2] DATETIME NULL
,    [fhLlegadaBodegaNacional] DATETIME NULL
,    [fhIngresoBodegaNacional] DATETIME NULL
,    [fhSalidaBodegaNacional] DATETIME NULL
,    [bodegaNacional] VARCHAR(150) NULL
,    [fhLlegadaCEBAF] DATETIME NULL
,    [fhCruceEcuador] DATETIME NULL
,    [fhAutorizacionNacionalizacion] DATETIME NULL
,    [bodegaEcuatoriana] VARCHAR(150) NULL
,    [fhLlegadaTCI] DATETIME NULL
,    [fhSalidaTCI] DATETIME NULL
,    [bodegaDescarga] VARCHAR(150) NULL
,    [fhLlegadaPlantaEcuador] DATETIME NULL
,    [fhLlegadaAlmacen] DATETIME NULL
,    [fhIngreso] DATETIME NULL
,    [fhInicioDescarga] DATETIME NULL
,    [fhTerminoDescarga] DATETIME NULL
,    [fhSalida] DATETIME NULL
,    [motivoRetraso] VARCHAR(1000) NULL
,    [sacosRobados] INT NOT NULL CONSTRAINT [DF__Seguimien__sacos__5026DB83] DEFAULT ((0))
,    [sacosRotos] INT NOT NULL CONSTRAINT [DF__Seguimien__sacos__511AFFBC] DEFAULT ((0))
,    [sacosMojados] INT NOT NULL CONSTRAINT [DF__Seguimien__sacos__520F23F5] DEFAULT ((0))
,    [estado] VARCHAR(20) NOT NULL CONSTRAINT [DF__Seguimien__estad__5303482E] DEFAULT ('EN_CURSO')
,    [fechaRegistro] DATETIME NOT NULL CONSTRAINT [DF__Seguimien__fecha__53F76C67] DEFAULT (getdate())
,    [idUsuarioRegistro] INT NULL
,    [fechaModificacion] DATETIME NULL
,    [idUsuarioModificacion] INT NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Seguimien__activ__54EB90A0] DEFAULT ((1))
,    CONSTRAINT [PK__Seguimie__1B37049CF4047E2B] PRIMARY KEY CLUSTERED ([idSeguimiento] ASC)
);
GO

IF OBJECT_ID('dbo.SubTramos', 'U') IS NULL
CREATE TABLE [dbo].[SubTramos] (

     [idSubTramo] INT IDENTITY(1,1) NOT NULL
,    [idLiquidacion] INT NOT NULL
,    [numeroSubTramo] INT NOT NULL
,    [origen] VARCHAR(100) NOT NULL
,    [destino] VARCHAR(100) NOT NULL
,    [tipoOperacion] VARCHAR(30) NOT NULL
,    [observaciones] VARCHAR(500) NULL
,    [guiaTransportista] VARCHAR(50) NULL
,    [guiaCliente] VARCHAR(50) NULL
,    [cruzaFrontera] BIT NULL CONSTRAINT [DF__SubTramos__cruza__4959E263] DEFAULT ((0))
,    [manifiesto] VARCHAR(50) NULL
,    [motivoParada] VARCHAR(50) NULL
,    [duracionHoras] INT NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__SubTramos__fecha__4A4E069C] DEFAULT (getdate())
,    [activo] BIT NOT NULL CONSTRAINT [DF__SubTramos__activ__4B422AD5] DEFAULT ((1))
,    CONSTRAINT [PK_SubTramos] PRIMARY KEY CLUSTERED ([idSubTramo] ASC)
,    CONSTRAINT [UK_SubTramo_Numero] UNIQUE NONCLUSTERED ([idLiquidacion] ASC, [numeroSubTramo] ASC)
);
GO

IF OBJECT_ID('dbo.TipoCarro', 'U') IS NULL
CREATE TABLE [dbo].[TipoCarro] (

     [idTipoCarro] INT IDENTITY(1,1) NOT NULL
,    [nombre] VARCHAR(10) NOT NULL
,    CONSTRAINT [PK__TipoCarr__C9C70FA7EE6B6FB1] PRIMARY KEY CLUSTERED ([idTipoCarro] ASC)
,    CONSTRAINT [UQ__TipoCarr__72AFBCC6B6E30506] UNIQUE NONCLUSTERED ([nombre] ASC)
);
GO

IF OBJECT_ID('dbo.Tracto', 'U') IS NULL
CREATE TABLE [dbo].[Tracto] (

     [idTracto] INT IDENTITY(1,1) NOT NULL
,    [placaTracto] VARCHAR(10) NULL
,    [modelo] VARCHAR(30) NULL
,    [marca] VARCHAR(30) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Tracto__activo__0CA5D9DE] DEFAULT ((1))
,    CONSTRAINT [PK__Tracto__ECFD9583654CE50B] PRIMARY KEY CLUSTERED ([idTracto] ASC)
);
GO

IF OBJECT_ID('dbo.UploadHistory', 'U') IS NULL
CREATE TABLE [dbo].[UploadHistory] (

     [UploadID] INT IDENTITY(1,1) NOT NULL
,    [FileName] NVARCHAR(255) NOT NULL
,    [UploadDate] DATETIME NOT NULL
,    [Month] INT NOT NULL
,    [Year] INT NOT NULL
,    [RowsProcessed] INT NOT NULL
,    [Status] NVARCHAR(50) NOT NULL
,    [ErrorCount] INT NULL
,    [UploadedBy] NVARCHAR(100) NULL
,    CONSTRAINT [PK__UploadHi__6D16C86D1183338C] PRIMARY KEY CLUSTERED ([UploadID] ASC)
);
GO

IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
CREATE TABLE [dbo].[Usuarios] (

     [idUsuario] INT IDENTITY(1,1) NOT NULL
,    [nombreUsuario] VARCHAR(50) NOT NULL
,    [contrasena] VARCHAR(100) NOT NULL
,    [nombre] VARCHAR(100) NULL
,    [apellido] VARCHAR(100) NULL
,    [correo] VARCHAR(100) NULL
,    [rol] VARCHAR(50) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__Usuarios__activo__367C1819] DEFAULT ((1))
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__Usuarios__fechaC__37703C52] DEFAULT (getdate())
,    [idConductor] INT NULL
,    [email] NVARCHAR(100) NULL
,    [resetToken] NVARCHAR(100) NULL
,    [resetTokenExpira] DATETIME NULL
,    [requiereCambioContrasena] BIT NULL CONSTRAINT [DF__Usuarios__requie__041093DD] DEFAULT ((0))
,    [idOperador] INT NULL
,    CONSTRAINT [PK__Usuarios__645723A64849A400] PRIMARY KEY CLUSTERED ([idUsuario] ASC)
,    CONSTRAINT [UQ__Usuarios__A0436BD70E17923A] UNIQUE NONCLUSTERED ([nombreUsuario] ASC)
);
GO

IF OBJECT_ID('dbo.ViajesEnProgreso', 'U') IS NULL
CREATE TABLE [dbo].[ViajesEnProgreso] (

     [idViajeProgreso] INT IDENTITY(1,1) NOT NULL
,    [numeroViajeProgreso] VARCHAR(20) NOT NULL
,    [idConductor] INT NOT NULL
,    [fechaInicio] DATETIME NOT NULL
,    [fechaUltimaActividad] DATETIME NOT NULL CONSTRAINT [DF__ViajesEnP__fecha__39AD8A7F] DEFAULT (getdate())
,    [estadoViaje] VARCHAR(15) NOT NULL CONSTRAINT [DF__ViajesEnP__estad__37C5420D] DEFAULT ('ABIERTO')
,    [descripcionViaje] VARCHAR(300) NULL
,    [cantidadDespachos] INT NOT NULL CONSTRAINT [DF__ViajesEnP__canti__36D11DD4] DEFAULT ((0))
,    [esInternacional] BIT NULL
,    [fechaCreacion] DATETIME NOT NULL CONSTRAINT [DF__ViajesEnP__fecha__38B96646] DEFAULT (getdate())
,    [usuarioCreacion] VARCHAR(50) NULL
,    [fechaCierre] DATETIME NULL
,    [usuarioCierre] VARCHAR(50) NULL
,    [idOrdenViajeGenerada] INT NULL
,    [observacionesCierre] VARCHAR(500) NULL
,    [activo] BIT NOT NULL CONSTRAINT [DF__ViajesEnP__activ__3AA1AEB8] DEFAULT ((1))
,    [motivoCierre] NVARCHAR(500) NULL
,    [observaciones] NTEXT NULL
,    CONSTRAINT [PK_ViajesEnProgreso] PRIMARY KEY CLUSTERED ([idViajeProgreso] ASC)
,    CONSTRAINT [UK_ViajesEnProgreso_Numero] UNIQUE NONCLUSTERED ([numeroViajeProgreso] ASC)
);
GO

IF OBJECT_ID('dbo.volquetes', 'U') IS NULL
CREATE TABLE [dbo].[volquetes] (

     [id] INT NOT NULL
,    [placa] VARCHAR(15) NOT NULL
,    [marca] VARCHAR(60) NOT NULL
,    [color] VARCHAR(40) NULL
,    [n_serie] VARCHAR(50) NULL
,    [cap_m3] VARCHAR(20) NULL
,    [anio] INT NULL
,    [potencia] VARCHAR(20) NULL
,    [ubicacion] VARCHAR(40) NULL
,    CONSTRAINT [PK__volquete__3213E83F8C1C361B] PRIMARY KEY CLUSTERED ([id] ASC)
);
GO

