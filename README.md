# Client Secret Expiry

## Summary

Gets all the applications in a tenant and loops through the client secret collection and categorizes each item based on the expity date:
- Expired: if less than 0 days
- Critical: if less than 14 days
- Warning: if less than 30 days but more than or equal to 14 days
- Ignore: if more than 30 days

An email notification is sent with a summary of the information

## Prerequisites

The following user accounts (as reflected in the app settings) are required:

| Account           | Membership requirements                               |
| ----------------- | ----------------------------------------------------- |
| emailUserName     | n/a                                                   |

Note that user account design can be modified to suit your environment

## Version 

![dotnet 8](https://img.shields.io/badge/net8.0-blue.svg)

## API permission

MSGraph

| API / Permissions name    | Type        | Admin consent | Justification                       |
| ------------------------- | ----------- | ------------- | ----------------------------------- |
| Application.Read.All      | Application | Yes           | Read all applications               |
| Mail.Send                 | Delegated   | Yes           | Send mail as a user                 | 
| User.Read                 | Delegated   | No            | Sign in and read user profile       |

Sharepoint

n/a

## App setting

| Name                    | Description                                                                    |
| ----------------------- | ------------------------------------------------------------------------------ |
| AzureWebJobsStorage     | Connection string for the storage acoount                                      |
| clientId                | The application (client) ID of the app registration                            |
| emailUserId			  | Object Id for the email user account                                           |
| emailUserName           | Email address used to send notifications                                       |
| emailUserSecret         | Secret name for emailUserSecret                                                |
| keyVaultUrl             | Address for the key vault                                                      |
| recipientAddress        | Email address(es) that receive notifications                                   |
| secretName              | Secret name used to authorize the function app                                 |
| tenantId                | Id of the Azure tenant that hosts the function app                             |

## Version history

Version|Date|Comments
-------|----|--------
1.0|TBD|Initial release

## Disclaimer

**THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.**
