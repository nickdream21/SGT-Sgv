<%@ Page Title="Importar seguimiento de exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ImportarSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.ImportarSeguimiento" %>

<asp:Content ID="ContentImportar" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <a class="sgv-volver" href="RegistroSeguimiento.aspx">Volver al tablero de seguimiento</a>
                <h1 class="h2">Importar seguimiento de exportación</h1>
                <p>Para cargar el STATUS GENERAL o la plantilla desde Excel. Las filas que ya existen (pedido + tracto + programación) se actualizan, no se duplican. Los viajes que vienen de despachos o se crearon a mano no se modifican.</p>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" EnableViewState="false" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" EnableViewState="false" />
        </asp:Panel>

        <div class="sgv-panel">
            <div class="sgv-panel-cab">
                <span class="sgv-panel-titulo">Archivo</span>
                <asp:Button ID="btnDescargarPlantilla" runat="server" Text="Descargar plantilla" CssClass="sx-btn sx-btn-chico" CausesValidation="false" OnClick="btnDescargarPlantilla_Click" data-descarga="true" />
            </div>
            <div class="sgv-panel-cuerpo sx-columna">
                <div id="zonaArchivo" class="sx-zona-archivo" role="button" tabindex="0">
                    <h4>Arrastra el archivo aquí o haz clic para elegirlo</h4>
                    <span class="sx-nota">Solo .xlsx, hasta 10 MB (un .xls antiguo: ábralo en Excel y guárdelo como .xlsx)</span>
                    <asp:FileUpload ID="fileExcel" runat="server" CssClass="d-none" aria-label="Archivo Excel de seguimiento" accept=".xlsx" />
                    <div id="infoArchivo" class="sx-nota sx-mt">Ningún archivo seleccionado</div>
                </div>
                <div class="sx-barra-acciones">
                    <asp:Button ID="btnImportar" runat="server" Text="Procesar archivo" CssClass="sx-btn sx-btn-primario" OnClick="btnImportar_Click" data-espera="300" />
                </div>
            </div>
        </div>

        <%-- Celdas que no se pudieron leer en la última importación (fila, columna, valor). --%>
        <asp:Panel ID="pnlReporteImportacion" runat="server" Visible="false" CssClass="sgv-panel mt-3">
            <div class="sgv-panel-cab"><span class="sgv-panel-titulo">Celdas que no se pudieron leer</span></div>
            <div class="sgv-panel-cuerpo">
                <p class="sx-nota">Se importó el resto de la fila; estos hitos quedaron vacíos. Corrígelos en el Excel y vuelve a subirlo.</p>
                <asp:Literal ID="litReporteImportacion" runat="server"></asp:Literal>
            </div>
        </asp:Panel>
    </div>

    <script>
        var SGV = Object.assign(window.SGV || {}, { fileExcel: '<%= fileExcel.ClientID %>' });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/ImportarSeguimiento.js") %>"></script>
</asp:Content>
