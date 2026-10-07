# Build: publica a API e as telas (Blazor WebAssembly) separadamente
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Evita segmentation fault (status 139) do .NET no ambiente de containers do Render
ENV DOTNET_EnableWriteXorExecute=0
WORKDIR /src

# Restaura primeiro (camada em cache enquanto os .csproj não mudarem)
COPY Chamados.sln ./
COPY src/Chamados.Api/Chamados.Api.csproj src/Chamados.Api/
COPY src/Chamados.Web/Chamados.Web.csproj src/Chamados.Web/
COPY src/Chamados.Shared/Chamados.Shared.csproj src/Chamados.Shared/
RUN dotnet restore src/Chamados.Api/Chamados.Api.csproj && dotnet restore src/Chamados.Web/Chamados.Web.csproj

COPY src/ src/
RUN dotnet publish src/Chamados.Api/Chamados.Api.csproj -c Release -o /out/api --no-restore
RUN dotnet publish src/Chamados.Web/Chamados.Web.csproj -c Release -o /out/web --no-restore

# Execução: a API serve a própria API e as telas (wwwroot) num único serviço
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV DOTNET_EnableWriteXorExecute=0
WORKDIR /app
COPY --from=build /out/api ./
COPY --from=build /out/web/wwwroot ./wwwroot

# O Render encaminha o tráfego para a porta definida em PORT (configure PORT=8080 no serviço)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Chamados.Api.dll"]
