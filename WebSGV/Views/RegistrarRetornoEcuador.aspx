<%@ Page Title="Registrar Retorno Ecuador" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistrarRetornoEcuador.aspx.cs" Inherits="WebSGV.Views.RegistrarRetornoEcuador" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .re-wrap {
            max-width: 900px;
            margin: 20px auto;
            background: #fff;
            border-radius: 8px;
            padding: 24px;
            box-shadow: 0 2px 8px rgba(0,0,0,.08);
        }
        .re-wrap h3 { color: #4a148c; margin-bottom: 4px; }
        .re-wrap .sub { color:#666; font-size:.9rem; margin-bottom: 18px; }
        .re-info {
            background: #f3e5f5;
            border-left: 4px solid #6a1b9a;
            padding: 10px 14px;
            border-radius: 6px;
            font-size: .9rem;
            margin-bottom: 18px;
        }
        .re-banner {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 10px;
            background: #fafafa;
            border: 1px solid #eee;
            border-radius: 8px;
            padding: 12px;
            margin-bottom: 16px;
        }
        .re-banner .label { font-size:.7rem; color:#888; text-transform: uppercase; }
        .re-banner .val { font-weight: 600; color: #333; }

        .tickets-head, .ticket-row {
            display: grid;
            grid-template-columns: 1.3fr 1.6fr 0.9fr 0.9fr 0.4fr;
            gap: 8px;
            align-items: center;
        }
        .tickets-head {
            font-size:.75rem; text-transform: uppercase; color:#666;
            padding: 6px 8px; background:#f5f5f5; border-radius:6px; margin-bottom:6px;
        }
        .ticket-row { padding: 4px 0; }
        .ticket-row input { width: 100%; }
        .btn-remove {
            background:#e53935; color:#fff; border:none; border-radius:6px;
            padding: 6px 10px; cursor:pointer; font-size:.8rem;
        }
        .btn-add {
            background:#43a047; color:#fff; border:none; border-radius:6px;
            padding: 8px 14px; cursor:pointer; font-weight:600; margin-top:8px;
        }
        .tot-box {
            display:flex; justify-content:flex-end; gap:24px;
            margin-top: 12px; font-size: 1rem;
        }
        .tot-box strong { color:#1565c0; }

        .re-actions { margin-top: 20px; display:flex; gap:10px; justify-content:flex-end; }
        .btn-save { background:#4a148c; color:#fff; border:none; border-radius:8px; padding:10px 20px; font-weight:600; }
        .btn-cancel { background:#eee; color:#333; border:none; border-radius:8px; padding:10px 20px; }
    </style>

    <div class="re-wrap">
        <h3><i class="fas fa-globe-americas"></i> Registrar Retorno Ecuador</h3>
        <div class="sub">Ingreso informativo de combustible comprado en Ecuador y recibido en el grifo.</div>

        <div class="re-info">
            <i class="fas fa-info-circle"></i>
            Este registro <strong>no es un abastecimiento al vehículo</strong>: son los galones que llegaron físicamente al grifo en los tanques del tracto y quedan como stock hasta la próxima programación.
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="alert alert-success" />
        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger" />

        <div class="re-banner">
            <div><div class="label">Viaje</div><div class="val"><asp:Literal ID="litNumeroViaje" runat="server" Text="-" /></div></div>
            <div><div class="label">Conductor</div><div class="val"><asp:Literal ID="litConductor" runat="server" Text="-" /></div></div>
            <div><div class="label">Tracto</div><div class="val"><asp:Literal ID="litTracto" runat="server" Text="-" /></div></div>
            <div><div class="label">Fecha Recepción</div><div class="val"><asp:TextBox ID="txtFecha" runat="server" TextMode="DateTimeLocal" CssClass="form-control" /></div></div>
        </div>

        <div class="tickets-head">
            <div>Nº Ticket</div>
            <div>Proveedor</div>
            <div>Galones</div>
            <div>Precio USD</div>
            <div></div>
        </div>
        <div id="ticketsContainer">
            <!-- filas dinamicas -->
        </div>
        <button type="button" class="btn-add" onclick="agregarTicket()">
            <i class="fas fa-plus"></i> Agregar ticket
        </button>

        <div class="tot-box">
            <span>Total galones: <strong id="totalGal">0.00</strong> GL</span>
            <span>Total USD: <strong>$<span id="totalUsd">0.00</span></strong></span>
        </div>

        <div style="margin-top:16px;">
            <label style="font-size:.85rem; color:#555;">Observaciones</label>
            <asp:TextBox ID="txtObservaciones" runat="server" TextMode="MultiLine" Rows="2" CssClass="form-control" />
        </div>

        <asp:HiddenField ID="hfIdViaje" runat="server" />
        <asp:HiddenField ID="hfIdConductor" runat="server" />
        <asp:HiddenField ID="hfIdTracto" runat="server" />
        <asp:HiddenField ID="hfTicketsJson" runat="server" />

        <div class="re-actions">
            <a href="DashboardGrifo.aspx" class="btn-cancel" style="text-decoration:none; display:inline-block;">Cancelar</a>
            <asp:Button ID="btnGuardar" runat="server" Text="Guardar Retorno" CssClass="btn-save"
                OnClientClick="return prepararEnvio();" OnClick="btnGuardar_Click" />
        </div>
    </div>

    <script type="text/javascript">
        var contador = 0;

        function agregarTicket() {
            contador++;
            var cont = document.getElementById('ticketsContainer');
            var row = document.createElement('div');
            row.className = 'ticket-row';
            row.id = 'row_' + contador;
            row.innerHTML =
                '<input type="text" class="form-control ticket-numero" maxlength="50" placeholder="Nº ticket" />' +
                '<input type="text" class="form-control ticket-proveedor" maxlength="150" placeholder="Proveedor / estación" />' +
                '<input type="number" step="0.01" min="0" class="form-control ticket-gal" placeholder="0.00" oninput="recalc()" />' +
                '<input type="number" step="0.01" min="0" class="form-control ticket-usd" placeholder="0.00" oninput="recalc()" />' +
                '<button type="button" class="btn-remove" onclick="quitar(\'row_' + contador + '\')"><i class="fas fa-trash"></i></button>';
            cont.appendChild(row);
        }

        function quitar(id) {
            var r = document.getElementById(id);
            if (r) r.parentNode.removeChild(r);
            recalc();
        }

        function recalc() {
            var gal = 0, usd = 0;
            document.querySelectorAll('.ticket-gal').forEach(function (i) { gal += parseFloat(i.value) || 0; });
            document.querySelectorAll('.ticket-usd').forEach(function (i) { usd += parseFloat(i.value) || 0; });
            document.getElementById('totalGal').innerText = gal.toFixed(2);
            document.getElementById('totalUsd').innerText = usd.toFixed(2);
        }

        function prepararEnvio() {
            var filas = document.querySelectorAll('#ticketsContainer .ticket-row');
            if (filas.length === 0) {
                alert('Agregue al menos un ticket.');
                return false;
            }
            var lista = [];
            for (var i = 0; i < filas.length; i++) {
                var f = filas[i];
                var num = f.querySelector('.ticket-numero').value.trim();
                var prov = f.querySelector('.ticket-proveedor').value.trim();
                var gal = parseFloat(f.querySelector('.ticket-gal').value) || 0;
                var usd = parseFloat(f.querySelector('.ticket-usd').value) || 0;
                if (gal <= 0) {
                    alert('Cada ticket debe tener galones > 0.');
                    return false;
                }
                lista.push({ numeroTicket: num, proveedor: prov, galones: gal, precioUSD: usd });
            }
            document.getElementById('<%= hfTicketsJson.ClientID %>').value = JSON.stringify(lista);
            return true;
        }

        // Primer ticket por defecto al cargar
        (function () { agregarTicket(); })();
    </script>
</asp:Content>
