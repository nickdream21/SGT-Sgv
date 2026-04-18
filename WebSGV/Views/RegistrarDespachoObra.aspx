<%@ Page Title="Registrar Despacho a Obra" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistrarDespachoObra.aspx.cs" Inherits="WebSGV.Views.RegistrarDespachoObra" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .do-container { background:#fff; border-radius:8px; padding:20px; box-shadow:0 .5rem 1rem rgba(0,0,0,.08); }
        .do-header { border-bottom:3px solid #e65100; padding-bottom:10px; margin-bottom:18px; color:#e65100; }
        .do-section { background:#fff8f3; border:1px solid #ffd7b3; border-left:4px solid #e65100; border-radius:6px; padding:14px; margin-bottom:16px; }
        .do-grid { display:grid; grid-template-columns: repeat(auto-fit, minmax(220px,1fr)); gap:12px; }
        .do-label { font-size:.78rem; font-weight:600; color:#555; text-transform:uppercase; }
        .entrega-card { background:#fff; border:1px solid #e0e0e0; border-left:4px solid #e65100; border-radius:6px; padding:12px; margin-bottom:10px; position:relative; }
        .entrega-num { position:absolute; top:-10px; left:10px; background:#e65100; color:#fff; padding:2px 10px; border-radius:10px; font-size:.75rem; font-weight:700; }
        .btn-remove-ent { background:#d32f2f; color:#fff; border:0; border-radius:4px; padding:4px 10px; font-size:.8rem; cursor:pointer; float:right; }
        .totales-box { background:#fff3e0; border:1px dashed #e65100; border-radius:6px; padding:12px; font-size:.95rem; }
        .totales-box strong { color:#e65100; font-size:1.1rem; }
    </style>

    <div class="do-container">
        <div class="do-header">
            <h3 style="margin:0;"><i class="fas fa-truck-moving mr-2"></i>Registrar Despacho de Combustible a Obra</h3>
            <small class="text-muted">Cisterna con combustible para maquinaria en obra</small>
        </div>

        <!-- Datos del despacho -->
        <div class="do-section">
            <h5 style="color:#e65100; margin-bottom:12px;"><i class="fas fa-info-circle mr-1"></i>Datos del Despacho</h5>
            <div class="do-grid">
                <div>
                    <div class="do-label">Cisterna (Tracto)</div>
                    <asp:DropDownList ID="ddlTracto" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Conductor</div>
                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Obra</div>
                    <asp:DropDownList ID="ddlObra" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Salida Grifo</div>
                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Llegada Obra (opcional)</div>
                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Retorno al Grifo (opcional)</div>
                    <asp:TextBox ID="txtFechaRetorno" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
            </div>
        </div>

        <!-- Medicion de combustible -->
        <div class="do-section">
            <h5 style="color:#e65100; margin-bottom:12px;"><i class="fas fa-gas-pump mr-1"></i>Medición de Combustible</h5>
            <div class="do-grid">
                <div>
                    <div class="do-label">Galones de Salida (llenado cisterna)</div>
                    <asp:TextBox ID="txtGalonesSalida" runat="server" CssClass="form-control"
                        TextMode="Number" step="0.01" onchange="recalcularTotales()" oninput="recalcularTotales()"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Galones de Retorno (al volver)</div>
                    <asp:TextBox ID="txtGalonesRetorno" runat="server" CssClass="form-control"
                        TextMode="Number" step="0.01" onchange="recalcularTotales()" oninput="recalcularTotales()"></asp:TextBox>
                </div>
            </div>
            <div class="totales-box" style="margin-top:12px;">
                <i class="fas fa-calculator mr-1"></i>
                Combustible abastecido en obra: <strong id="lblAbastecido">0.00</strong> GL
                <small class="text-muted d-block mt-1">
                    (salida − retorno = combustible entregado a las máquinas)
                </small>
            </div>
            <div style="margin-top:12px;">
                <div class="do-label">Observaciones</div>
                <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"></asp:TextBox>
            </div>
        </div>

        <div style="text-align:right; margin-top:18px;">
            <asp:Button ID="btnCancelar" runat="server" Text="Cancelar" CssClass="btn btn-outline-secondary mr-2"
                OnClick="btnCancelar_Click" CausesValidation="false" />
            <asp:Button ID="btnGuardar" runat="server" Text="Registrar Despacho" CssClass="btn btn-warning"
                OnClick="btnGuardar_Click" />
        </div>
    </div>

    <script type="text/javascript">
        function recalcularTotales() {
            var s = parseFloat(document.getElementById('<%= txtGalonesSalida.ClientID %>').value);
            var r = parseFloat(document.getElementById('<%= txtGalonesRetorno.ClientID %>').value);
            if (isNaN(s)) s = 0;
            if (isNaN(r)) r = 0;
            var abast = s - r;
            if (abast < 0) abast = 0;
            document.getElementById('lblAbastecido').innerText = abast.toFixed(2);
        }
    </script>
</asp:Content>
