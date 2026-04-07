# Use the official .NET SDK image to build the app (Updated to 10.0)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the code and build it
COPY . ./
RUN dotnet publish -c Release -o out

# Use the lighter runtime image to run the app (Updated to 10.0)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Tell Render to listen on port 8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "QRMenuAPI.dll"]