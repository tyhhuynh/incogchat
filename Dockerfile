# ---------- build stage ----------
    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    
    # Copy solution + project files first (better layer caching)
    COPY incogchat-server.sln ./
    COPY src/IncogChat.Server/*.csproj ./src/IncogChat.Server/
    
    # Restore
    RUN dotnet restore ./incogchat-server.sln
    
    # Copy the rest of the source
    COPY . .
    
    # Publish (no app host so the image stays smaller)
    RUN dotnet publish ./src/IncogChat.Server/IncogChat.Server.csproj \
        -c Release -o /app /p:UseAppHost=false
    
    # ---------- runtime stage ----------
    FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
    WORKDIR /app
    
    # (Optional) run non-root
    # RUN adduser --disabled-password --gecos "" app && chown -R app:app /app
    # USER app
    
    COPY --from=build /app ./
    EXPOSE 8080
    
    CMD ["dotnet", "IncogChat.Server.dll"]
    