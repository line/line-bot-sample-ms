# Introduction

The ASP.NET Core Linebot for Hackathon application.

# Getting Started

## Open solution or project

Open solution(sln) file in the Visual Studio 2017 or project(csproj) file in the Visual Studio Code.
You can also open in the command prompt or terminal.

## Configuration

Configure properties of the LINE Messaging API by one of environment variables, appsettings.json and user secrets.
Database server is required to run. MySQL for Development environment. Microsoft SQL Server for Azule enviromnent.
This will be setup by Startup.cs so you can switch to another database server.

### Environment variables

Set environment variables as shown in the following example.

On Windows.
```
ConnectionStrings:MainDatabase=[Your database connection string]
Line:ChannelId=[Your channel id]
Line:ChannelSecret=[Your channel secret]
Line:ChannelAccessToken=[Your channel access token]
Line:WebhookPath=[Your webhook path]
Microsoft:CognitiveService:Face:SubscriptionKey=[Your subscription key of the Microsoft Congnitive Service Face]
```

On Mac OS X / Linux.
```
$ export ConnectionStrings__MainDatabase=[Your database connection string]
$ export Line__ChannelId=[Your channel id]
$ export Line__ChannelSecret=[Your channel secret]
$ export Line__ChannelAccessToken=[Your channel access token]
$ export Line__WebhookPath=[Your webhook path]
$ export Microsoft__CognitiveService__Face__SubscriptionKey=[Your subscription key of the Microsoft Congnitive Service Face]
```
*(Note: Double underscore)*

### appsettings.json

Edit the appsettings.json file as shown in the following example.

***Caution:*** Please be careful not to commit or publish your confidential information to the Internet. It is strongly recommended that you store your confidential information as environment variables.

```
{
  "Logging": {
    "IncludeScopes": false,
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "ConnectionStrings": {
    "MainDatabase": "[Your database connection string]"
  },
  "Line": {
    "ChannelId": "[Your channel id]",
    "ChannelSecret": "[Your channel secret]",
    "ChannelAccessToken": "[Your channel access token]",
    "WebhookPath":  "[Your webhook path]"
  },
  "Microsoft": {
    "CognitiveService": {
      "Face": {
        "SubscriptionKey": "[Your subscription key of the Microsoft Congnitive Service Face]"
      }
    }
  }
}
```

### User secrets

It can also be stored as user secrets in the development environment.

## Restore, Build and Run

Restore packages, build and start debugging in the Visual Studio 2017 or Visual Studio Code.
You can also use the following dotnet command in the project directory.

```
$ dotnet restore
$ dotnet build
$ dotnet run
```

## Deploy

Finally, you can deploy this application to the App Service of Microsoft Azule.

# Disclaimer

This application was developed for demonstration purpose of Hackathon.
Use of this application is at your own risk.

