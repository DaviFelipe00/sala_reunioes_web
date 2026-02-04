# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Otimização de cache: copia apenas o arquivo de projeto primeiro
COPY ["SalaReunioes.Web.csproj", "./"]
RUN dotnet restore "./SalaReunioes.Web.csproj"

# Copia o restante dos arquivos
COPY . .

# Publica a aplicação
RUN dotnet publish "SalaReunioes.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# ==========================================
# Configuração de Localização no Container
# ==========================================
# Define que o .NET deve usar a cultura pt-BR por padrão no sistema operacional do container
ENV LANG=pt_BR.UTF-8
ENV LC_ALL=pt_BR.UTF-8
# Garante que o .NET não use o modo invariante de globalização (que força o inglês)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

# Mantendo a segurança com usuário não-root
USER app
ENTRYPOINT ["dotnet", "SalaReunioes.Web.dll"]