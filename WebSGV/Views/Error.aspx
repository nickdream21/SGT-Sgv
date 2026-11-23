<%@ Page Title="Error del Sistema" Language="C#" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="WebSGV.Views.Error" %>
<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Error del sistema</title>
    <style>
        body { font-family: "Segoe UI", Roboto, sans-serif; background: #eceef1; margin: 0; padding: 0; color: #1a2230; }
        .error-container { max-width: 900px; margin: 40px auto; padding: 24px; background: #fff; border: 1px solid #d5dae2; border-radius: 3px; }
        .error-icon { font-size: 4rem; color: #9a1c1c; text-align: center; margin-bottom: 12px; }
        .error-title { font-size: 1.5rem; font-weight: 700; text-align: center; margin: 0 0 8px; }
        .error-message { font-size: 1rem; color: #566173; text-align: center; margin-bottom: 18px; }
        .error-debug { background: #f8eed8; border: 1px solid #dcc48a; border-radius: 3px; padding: 14px; margin: 14px 0; word-break: break-word; }
        .error-debug strong { color: #7a5200; }
        .error-debug code { background: #fff; padding: 2px 6px; border-radius: 3px; }
        .error-debug pre { font-size: 0.78rem; margin: 8px 0 0; white-space: pre-wrap; background: #f5f6f8; padding: 10px; border-radius: 3px; max-height: 320px; overflow: auto; }
        .error-actions { text-align: center; margin-top: 18px; }
        .error-actions a { display: inline-block; margin: 4px; padding: 9px 18px; border-radius: 3px; text-decoration: none; font-weight: 500; }
        .error-btn-primario { background: #1f3a6e; color: #fff; }
        .error-btn-secundario { background: #fff; border: 1px solid #a8b0bc; color: #3b4656; }
        .error-code { text-align: center; color: #566173; font-size: 0.85rem; margin-top: 12px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="error-container">
            <div class="error-icon">&#9888;</div>
            <h1 class="error-title">Ocurri&oacute; un error inesperado</h1>
            <p class="error-message">Algo sali&oacute; mal al procesar tu solicitud.</p>

            <% if (!string.IsNullOrEmpty(ErrorDetalle)) { %>
            <div class="error-debug">
                <div><strong>Tipo:</strong> <code><%= ErrorTipo %></code></div>
                <div style="margin-top:6px;"><strong>Mensaje:</strong> <code><%= ErrorDetalle %></code></div>
                <% if (!string.IsNullOrEmpty(ErrorStack)) { %>
                <details style="margin-top:8px;">
                    <summary style="cursor:pointer; color:#856404; font-weight:bold;">Stack trace (click para expandir)</summary>
                    <pre><%= ErrorStack %></pre>
                </details>
                <% } %>
            </div>
            <% } else { %>
            <div class="error-debug">
                <strong>Sin detalles disponibles.</strong> No se pudo recuperar la excepci&oacute;n original.
                Revisa la ventana <em>Output &gt; Debug</em> de Visual Studio para ver el log detallado.
            </div>
            <% } %>

            <div class="error-actions">
                <a href="javascript:history.back()" class="error-btn-secundario">&larr; Volver</a>
                <a href="/Views/Login.aspx" class="error-btn-primario">Ir al inicio</a>
            </div>
            <p class="error-code">Error 500 &mdash; <%= WebSGV.Helpers.FechaHelper.Ahora().ToString("dd/MM/yyyy HH:mm") %></p>
        </div>
    </form>
</body>
</html>
