# Blazor Weather App

https://blazor-weather-app.azurewebsites.net/

Simple Blazor Web application that reads current weather data from an api and displays today's historical averages as well as the climate chart for the selected station.

## Technologies Used
* C#
* .NET 10.0
* Blazor Web App
* RestAPI
* Syncfusion Blazor Components

## Local configuration
Set the Syncfusion license key outside the repo via user secrets, app configuration, or the `SYNCFUSION_LICENSE_KEY` environment variable.

## Build and test
Use the XML solution file format introduced by the .NET 10 SDK:

```powershell
dotnet build .\BlazorWeatherApp.slnx -c Release
dotnet test .\BlazorWeatherApp.slnx -c Release
```

## To-Do List

* Ask for permission to use current location
* ~~Add climate chart similar to Wikipedia~~
* ~~Check latitude and longitude values for validity~~
* ~~Add precipitation to chart~~
