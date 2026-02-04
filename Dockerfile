# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# --- CORREÇÃO AQUI ---
# Antes procurava em "SalaReunioes.Web/...", agora pega da raiz "./"
COPY ["SalaReunioes.Web.csproj", "./"]
RUN dotnet restore "./SalaReunioes.Web.csproj"

# Copia todo o resto da raiz para dentro da imagem
COPY . .

# Como os arquivos já estão na raiz do /src, não precisamos dar 'cd' (WORKDIR)
RUN dotnet publish "SalaReunioes.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .

USER app
ENTRYPOINT ["dotnet", "SalaReunioes.Web.dll"]