# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SalaReunioes.Web.csproj", "./"]
RUN dotnet restore "./SalaReunioes.Web.csproj"

COPY . .
RUN dotnet publish "SalaReunioes.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# ==========================================
# 1. Configuração de Timezone (CORREÇÃO)
# ==========================================
# Instala o pacote tzdata (necessário para o Linux reconhecer os fusos)
# O USER root é necessário para instalar pacotes
USER root
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*

# Define o fuso horário para Recife
ENV TZ=America/Recife

# ==========================================
# Configuração de Localização
# ==========================================
ENV LANG=pt_BR.UTF-8
ENV LC_ALL=pt_BR.UTF-8
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

# Volta para o usuário seguro após a instalação
USER app
ENTRYPOINT ["dotnet", "SalaReunioes.Web.dll"]