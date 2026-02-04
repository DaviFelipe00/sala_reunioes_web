# Estágio 1: Build
# Usamos a imagem SDK completa para compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas o arquivo de projeto primeiro para aproveitar o cache de camadas do Docker
COPY ["SalaReunioes.Web/SalaReunioes.Web.csproj", "SalaReunioes.Web/"]
RUN dotnet restore "SalaReunioes.Web/SalaReunioes.Web.csproj"

# Copia o restante do código
COPY . .
WORKDIR "/src/SalaReunioes.Web"

# Compila e publica em modo Release
RUN dotnet publish "SalaReunioes.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime
# Usamos a imagem ASP.NET Core (mais leve, sem compilador) para rodar
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .

# Define o usuário para não rodar como root (Segurança)
USER app
ENTRYPOINT ["dotnet", "SalaReunioes.Web.dll"]