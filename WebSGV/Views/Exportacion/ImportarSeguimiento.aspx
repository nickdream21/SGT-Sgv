<%@ Page Title="Importar seguimiento de exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ImportarSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.ImportarSeguimiento" %>

<asp:Content ID="ContentImportar" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <div class="sgv-encabezado">
            <div>
                <h2>Importar seguimiento desde Excel</h2>
                <p>Para cargar el STATUS GENERAL o la plantilla. Las filas que ya existen (pedido + tracto + programación) se actualizan, no se duplican.</p>
            </div>
            <a href="RegistroSeguimiento.aspx" class="sx-btn">← Volver al tablero</a>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" />
        </asp:Panel>

        <div class="sgv-panel">
            <div class="sgv-panel-cab">
                <span class="sgv-panel-titulo">Archivo</span>
                <asp:Button ID="btnDescargarPlantilla" runat="server" Text="Descargar plantilla" CssClass="sx-btn sx-btn-chico" CausesValidation="false" OnClick="btnDescargarPlantilla_Click" />
            </div>
            <div class="sgv-panel-cuerpo" style="display: flex; flex-direction: column; gap: 1rem;">
                <div id="zonaArchivo" class="sx-zona-archivo" role="button" tabindex="0">
                    <i class="fas fa-file-excel" aria-hidden="true" style="font-size: 1.75rem; color: #15803d;"></i>
                    <h4>Arrastra el archivo aquí o haz clic para elegirlo</h4>
                    <span class="sx-nota">.xlsx o .xls, hasta 10 MB</span>
                    <asp:FileUpload ID="fileExcel" runat="server" CssClass="d-none" accept=".xlsx,.xls" />
                    <div id="infoArchivo" class="sx-nota" style="margin-top: 0.75rem;">Ningún archivo seleccionado</div>
                </div>
                <div class="sx-barra-acciones">
                    <asp:Button ID="btnImportar" runat="server" Text="Procesar archivo" CssClass="sx-btn sx-btn-primario" OnClick="btnImportar_Click" />
                </div>
            </div>
        </div>

        <%-- Celdas que no se pudieron leer en la última importación (fila, columna, valor). --%>
        <asp:Panel ID="pnlReporteImportacion" runat="server" Visible="false" CssClass="sgv-panel" style="margin-top: 1rem;">
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
