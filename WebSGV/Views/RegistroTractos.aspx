<%@ Page Title="Registro de Tracto" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroTractos.aspx.cs" Inherits="WebSGV.Views.RegistroTractos" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex justify-content-center align-items-center vh-100">
        <div class="card registro-tracto-card w-50 shadow-lg">
            <div class="card-header text-center registro-tracto-header">
                <h2 class="header mb-0">Registro de Tracto</h2>
            </div>
            <div class="card-body p-4">
                <!-- Campo N° Placa -->
                <div class="row mb-3">
                    <div class="col-md-12 form-group">
                        <label for="txtPlaca" class="form-label">
                            <i class="fas fa-car"></i>N° Placa:
                        </label>
                        <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control" placeholder="Ingrese N° de placa"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca" 
                            ErrorMessage="La placa es requerida" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                    </div>
                </div>
                <!-- Campo Modelo -->
                <div class="row mb-3">
                    <div class="col-md-12 form-group">
                        <label for="txtModelo" class="form-label">
                            <i class="fas fa-cogs"></i>Modelo:
                        </label>
                        <asp:TextBox ID="txtModelo" runat="server" CssClass="form-control" placeholder="Ingrese modelo"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvModelo" runat="server" ControlToValidate="txtModelo" 
                            ErrorMessage="El modelo es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                    </div>
                </div>
                <!-- Campo Marca -->
                <div class="row mb-3">
                    <div class="col-md-12 form-group">
                        <label for="txtMarca" class="form-label">
                            <i class="fas fa-industry"></i>Marca:
                        </label>
                        <asp:TextBox ID="txtMarca" runat="server" CssClass="form-control" placeholder="Ingrese marca"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvMarca" runat="server" ControlToValidate="txtMarca" 
                            ErrorMessage="La marca es requerida" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                    </div>
                </div>
                
                <!-- Mensajes de resultado -->
                <div class="alert alert-success" id="mensajeExito" runat="server" visible="false">
                    Tracto registrado exitosamente
                </div>
                <div class="alert alert-danger" id="mensajeError" runat="server" visible="false">
                    <asp:Literal ID="litError" runat="server"></asp:Literal>
                </div>
                
                <!-- Botón -->
                <div class="text-center">
                    <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg btn-block registro-tracto-btn" 
                        Text="Registrar" OnClick="btnRegistrar_Click" />
                </div>
            </div>
        </div>
    </div>
</asp:Content>