<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ReporteResultado.aspx.cs" Inherits="WebSGV.Views.ReporteResultado" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Reporte Generado</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css" rel="stylesheet" />
      <!-- CSS -->
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css" rel="stylesheet" />
  <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" rel="stylesheet" />
  <link href="https://code.jquery.com/ui/1.13.2/themes/base/jquery-ui.css" rel="stylesheet" />
  <link href="/Content/App.css" rel="stylesheet" />
</head>
<body>
    <form id="form1" runat="server" class="container mt-4">
        <h2 class="text-center mb-4">Resultado del Reporte</h2>
        <asp:Label ID="lblMensaje" runat="server" CssClass="alert alert-warning" Visible="false"></asp:Label>
        <asp:GridView ID="gvReporteVista" runat="server" CssClass="table table-bordered table-hover"></asp:GridView>
    </form>
</body>
</html>
