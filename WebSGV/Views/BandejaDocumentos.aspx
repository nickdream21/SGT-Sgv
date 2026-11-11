<%@ Page Title="Bandeja de documentos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BandejaDocumentos.aspx.cs" Inherits="WebSGV.Views.BandejaDocumentos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/BandejaDocumentos.css") %>" rel="stylesheet" />

    <div class="container-fluid px-3 bd-pagina">

        <div class="bd-cabecera">
            <div>
                <h2 class="bd-titulo"><i class="fas fa-inbox mr-2"></i>Bandeja de documentos</h2>
                <p class="text-muted mb-0">
                    Suelte aqu&#xED; todo lo que lleg&#xF3; por correo: el sistema reconoce cada documento, lo une con su carga y le muestra
                    qu&#xE9; va a registrar antes de guardar. Sin escribir n&#xFA;meros a mano.
                </p>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" EnableViewState="false" />
        </asp:Panel>

        <%-- ===== 1. Subir ===== --%>
        <asp:Panel ID="pnlSubir" runat="server" CssClass="bd-card bd-subir" DefaultButton="btnLeer">
            <label class="bd-zona" id="bdZona" for="<%= fuArchivos.ClientID %>">
                <i class="fas fa-cloud-upload-alt bd-zona-icono"></i>
                <span class="bd-zona-titulo">Arrastre aqu&#xED; los archivos o haga clic para elegirlos</span>
                <span class="bd-zona-ayuda">
                    ZIP del correo del cliente (factura, packing list y gu&#xED;as) &#xB7; PDF sueltos de un pedido &#xB7;
                    CPIC y manifiestos (MCI) &#xB7; facturas de flete de SUNAT (E001) &#xB7; nuestras gu&#xED;as transportista de SUNAT (GRE EG03),
                    la base de los clientes que no mandan factura. Puede subir varios a la vez.
                </span>
                <span class="bd-zona-archivos" id="bdArchivos"></span>
                <asp:FileUpload ID="fuArchivos" runat="server" AllowMultiple="true" accept=".zip,.pdf" CssClass="bd-zona-input" />
            </label>
            <div class="bd-subir-pie">
                <asp:Button ID="btnLeer" runat="server" Text="Leer documentos" CssClass="btn btn-primary bd-btn-leer" OnClick="btnLeer_Click" />
            </div>
        </asp:Panel>

        <%-- ===== 2. Vista previa ===== --%>
        <asp:Panel ID="pnlVistaPrevia" runat="server" Visible="false" CssClass="bd-card bd-previa">
            <div class="bd-previa-cabecera">
                <div>
                    <h5 class="mb-1"><i class="fas fa-clipboard-check mr-2"></i>Revise antes de registrar</h5>
                    <asp:Literal ID="litResumenPrevia" runat="server" EnableViewState="false" />
                </div>
                <div class="bd-previa-acciones">
                    <asp:Button ID="btnDescartar" runat="server" Text="Descartar" CssClass="btn btn-outline-secondary" OnClick="btnDescartar_Click" CausesValidation="false" />
                    <asp:Button ID="btnRegistrar" runat="server" Text="Registrar" CssClass="btn btn-success bd-btn-registrar" OnClick="btnRegistrar_Click" />
                </div>
            </div>
            <asp:Literal ID="litVistaPrevia" runat="server" EnableViewState="false" />
        </asp:Panel>

        <%-- ===== 3a. Expediente de un pedido (?lote=): toda su documentación con el PDF de cada documento ===== --%>
        <asp:Panel ID="pnlExpediente" runat="server" Visible="false" CssClass="bd-card bd-expediente">
            <asp:Literal ID="litExpediente" runat="server" EnableViewState="false" />
        </asp:Panel>

        <%-- ===== 3b. Tablero de pendientes ===== --%>
        <asp:Panel ID="pnlTablero" runat="server" CssClass="bd-card">
            <div class="bd-tablero-cabecera">
                <h5 class="mb-0"><i class="fas fa-tasks mr-2"></i>Documentos por cliente y pedido</h5>
                <asp:Literal ID="litContadorTablero" runat="server" EnableViewState="false" />
            </div>
            <asp:Panel ID="pnlFiltros" runat="server" CssClass="bd-filtros" DefaultButton="btnBuscar">
                <div class="bd-filtro">
                    <label for="<%= ddlCliente.ClientID %>">Cliente</label>
                    <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control form-control-sm"></asp:DropDownList>
                </div>
                <div class="bd-filtro bd-filtro-ancho">
                    <label for="<%= txtBuscar.ClientID %>">Pedido, factura o chofer</label>
                    <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="60" placeholder="4400134530, F222-00008598, REYES"></asp:TextBox>
                </div>
                <div class="bd-filtro">
                    <label for="<%= txtDesde.ClientID %>">Desde</label>
                    <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" CssClass="form-control form-control-sm"></asp:TextBox>
                </div>
                <div class="bd-filtro">
                    <label for="<%= txtHasta.ClientID %>">Hasta</label>
                    <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" CssClass="form-control form-control-sm"></asp:TextBox>
                </div>
                <div class="bd-filtro">
                    <label for="<%= ddlVer.ClientID %>">Ver</label>
                    <asp:DropDownList ID="ddlVer" runat="server" CssClass="form-control form-control-sm">
                        <asp:ListItem Value="PENDIENTES" Text="Con documentos pendientes"></asp:ListItem>
                        <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                        <asp:ListItem Value="COMPLETOS" Text="Completos"></asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="bd-filtro bd-filtro-boton">
                    <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-primary btn-sm" OnClick="btnBuscar_Click" CausesValidation="false" />
                </div>
            </asp:Panel>
            <asp:Literal ID="litTablero" runat="server" EnableViewState="false" />
        </asp:Panel>
    </div>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/BandejaDocumentos.js") %>"></script>
</asp:Content>
