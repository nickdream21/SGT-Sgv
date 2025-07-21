<%@ Page Title="Inicio" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Inicio.aspx.cs" Inherits="WebSGV.Views.Inicio" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
    /* Estilos para la página de inicio */
    .inicio-container {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 3rem 1rem;
        min-height: 80vh;
        background: linear-gradient(to bottom, #f8f9fa, #e9ecef);
        border-radius: 10px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.05);
        margin: 20px;
        transition: all 0.3s ease;
    }
    
    .logo {
        max-width: 250px;
        margin-bottom: 3rem;
        animation: float 3s ease-in-out infinite;
        filter: drop-shadow(0 10px 15px rgba(0,0,0,0.1));
    }
    
    @keyframes float {
        0% { transform: translateY(0px); }
        50% { transform: translateY(-10px); }
        100% { transform: translateY(0px); }
    }
    
    .inicio-titulo {
        font-size: 2.8rem;
        font-weight: 700;
        color: #2c3e50;
        margin-bottom: 1.5rem;
        text-align: center;
        letter-spacing: -0.5px;
    }
    
    .inicio-descripcion {
        font-size: 1.3rem;
        color: #6c757d;
        text-align: center;
        max-width: 700px;
        line-height: 1.6;
        animation: fadeIn 1s ease-in;
    }
    
    @keyframes fadeIn {
        from { opacity: 0; transform: translateY(20px); }
        to { opacity: 1; transform: translateY(0); }
    }
    
    /* Añadir efecto de hover para interactividad */
    .inicio-container:hover {
        box-shadow: 0 8px 30px rgba(0,0,0,0.1);
        transform: translateY(-5px);
    }
    
    /* Media query para dispositivos móviles */
    @media (max-width: 768px) {
        .inicio-titulo {
            font-size: 2rem;
        }
        
        .inicio-descripcion {
            font-size: 1.1rem;
        }
        
        .logo {
            max-width: 180px;
        }
    }
</style>
    <div class="inicio-container">
        <img src="/Content/favicon.png" alt="Logo SGV" class="logo">
        <h1 class="inicio-titulo">Bienvenido al Sistema SGV</h1>
        <p class="inicio-descripcion">
            Gestiona de manera eficiente tus operaciones de transporte y logística.
        </p>
    </div>
</asp:Content>
