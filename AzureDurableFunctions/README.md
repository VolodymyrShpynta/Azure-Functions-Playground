# Azure Durable Functions — Managed Identity Role Setup

When you create an Azure Function App using **Visual Studio Code**, the Azure Functions extension automatically creates and attaches a **Managed Identity** to your Function App.  
This identity is generated to enable **secure, passwordless access** to Azure resources such as Storage Accounts without storing secrets or connection strings in your application settings.

## Why is the Managed Identity needed?
Durable Functions uses Azure Storage for its internal state management. It requires access to:
- **Blob Storage** (for large messages and history)
- **Queue Storage** (for orchestration work items)
- **Table Storage** (for instance and history tables)

The Managed Identity allows your Function App to authenticate to these services using Azure Active Directory instead of keys, improving security and simplifying credential management.

## Roles that must be assigned
To allow Durable Functions to work correctly, you must grant the following **data-plane roles** to the Managed Identity that VS Code created:

- **Storage Blob Data Contributor**
- **Storage Queue Data Contributor**
- **Storage Table Data Contributor**

> These roles must be assigned **to the Managed Identity itself**, not to the Function App resource. Without these roles, Durable Functions will fail with errors like `AuthorizationPermissionMismatch (403)` when trying to create or access its Task Hub tables and queues.

## How to assign roles in Azure Portal
1. Go to **Function App → Identity** and confirm the Managed Identity (System-Assigned or User-Assigned) created by VS Code.
2. Navigate to your **Storage Account → Access control (IAM)**.
3. Click **Add role assignment** and assign the following roles:
   - Storage Blob Data Contributor
   - Storage Queue Data Contributor
   - Storage Table Data Contributor
4. In the **Members** section, select **Managed Identity** and choose the identity linked to your Function App.
5. Scope: **This resource** (the storage account).
6. Save changes and allow a few minutes for RBAC propagation.

## How to assign roles using Azure CLI
Replace placeholders and run:

```bash
SUBSCRIPTION_ID="<subscription-id>"
RESOURCE_GROUP="<resource-group>"
STORAGE_ACCOUNT="<storage-account-name>"
MI_PRINCIPAL_ID="<managed-identity-principal-id>"

SCOPE="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Storage/storageAccounts/${STORAGE_ACCOUNT}"

az role assignment create --assignee-object-id "${MI_PRINCIPAL_ID}" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" --scope "${SCOPE}"

az role assignment create --assignee-object-id "${MI_PRINCIPAL_ID}" --assignee-principal-type ServicePrincipal \
  --role "Storage Queue Data Contributor" --scope "${SCOPE}"

az role assignment create --assignee-object-id "${MI_PRINCIPAL_ID}" --assignee-principal-type ServicePrincipal \
  --role "Storage Table Data Contributor" --scope "${SCOPE}"
