---
title: OAuth2
category: Documentaion
categoryindex: 1
index: 2
---

Any 3rd party APIs that use OAuth2 will require two key pieces before you can access them:

 - A client, sometimes also called an app(lication), representing your project
 - A user, who has authorized your app to access their data

Both can usually be represented as a JSON object with a handful of keys. In this library, they are represented as `OAuth2App` and `OAuth2User` respectively. The `OAuth2` class is a wrapper around a set of these two objects, and so can complete requests on its own.

## Creating an OAuth2 object

There are two provided constructors for `OAuth2`, one that takes two `OAuth2App` and `OAuth2User` objects, and one that takes two paths to JSON files. The second is convenient for a quick start, but the first is more appropriate when you are granting many users access to your app. You can also use `OAuth2App.WithUser`, which just calls the first constructor mentioned above.

## Getting app credentials

Each website has its own process for creating app/client credentials for your project. Here are some common ones:

 - [Facebook](https://developers.facebook.com/docs/apps/register)
 - [Google](https://console.cloud.google.com)
 - [Discord](https://discord.com/developers)

Usually after signing up, perhaps getting approved, and making a project, you can download or copy a JSON file with an ID and a secret token. Note that in some cases, such as many Google APIs, you will need to enable the API in the console before you can use it, and it may require a billing account or approval.

## Getting user credentials

To grant just your own user account for a bot or testing purposes, you can use the command line tool provided by the Python version of this package, [SlyAPI-Python](https://pypi.org/project/SlyAPI/). Note that this package will require you to install Python 3.10 or newer.

For authorizing other users in code, this library implements [OAuth2 with PKCE extension](https://oauth.net/2/pkce/).
The `OAuth2` class has two methods that generate part of the process for authorization.

In addition, a helper class is provided that can be used to generate credentials in a simple way with less control; the `PkceOAuth2Wizard`. Note that you must be able to direct the user to a 3rd party webpage and be able to accept the user and data when they return to your app between steps.

For more information on the process, see the [OAuth2 documentation](https://oauth.net/2/).