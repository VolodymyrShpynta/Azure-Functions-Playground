# Azure Durable Functions — Deployment & Managed Identity Role Setup

When you create an Azure Function App using **Visual Studio Code**, the Azure Functions extension automatically creates and attaches a **Managed Identity** to your Function App.  
This identity enables **secure, passwordless access** to Azure resources such as Storage Accounts without storing secrets or connection strings in your application settings.

---

## Why is the Managed Identity needed?
Durable Functions uses Azure Storage for its internal state management. It requires access to:
- **Blob Storage** (for large messages and history)
- **Queue Storage** (for orchestration work items)
- **Table Storage** (for instance and history tables)

The Managed Identity allows your Function App to authenticate to these services using Azure Active Directory instead of keys, improving security and simplifying credential management.

---

## Roles that must be assigned
To allow Durable Functions to work correctly, you must grant the following **data-plane roles** to the Managed Identity:

- **Storage Blob Data Contributor**
- **Storage Queue Data Contributor**
- **Storage Table Data Contributor**

> These roles must be assigned **to the Managed Identity itself**, not to the Function App resource. Without these roles, Durable Functions will fail with errors like `AuthorizationPermissionMismatch (403)` when trying to create or access its Task Hub tables and queues.

---

## Full Deployment Manual Steps (if deployment via VS Code is not an option)

### Step 1: Build and Package the Function
```bash
dotnet build --configuration Release
dotnet publish --configuration Release --output ./publish
cd publish
zip -r ../function.zip .
```

### Step 2: Create Resources

#### **Option A: Azure Portal**

1. Create Resource Group
    - Go to *Azure Portal* → *Resource Groups* → *+ Create*.
    - Select *Subscription*, enter *Resource Group Name*, and choose *Region*.

2. Create Function App
    - Go to *Azure Portal* → *Create Resource* → *Function App*.
    - Select:
      - *Subscription* and *Resource Group*.
      - *Function App Name* (must be globally unique).
      - *Publish*: Code.
      - *Runtime stack*: .NET.
      - *Version*: 4.
      - *Region*: Same as Resource Group.
    - *Hosting*:
      - Create a new *Storage Account*.
      - Choose Plan: *Consumption* or *Premium*.
    - Identity:
      - Enable *System-Assigned Managed Identity*.
    - (Optional) Enable *Application Insights*.
    - Click *Review + Create* → *Create*.

#### **Option B: Azure CLI**

```Shell
az login
az account set --subscription "<TARGET_SUBSCRIPTION_ID>"

az group create --name <RESOURCE_GROUP_NAME> --location <LOCATION>

az storage account create \
  --name <STORAGE_ACCOUNT_NAME> \
  --location <LOCATION> \
  --resource-group <RESOURCE_GROUP_NAME> \
  --sku Standard_LRS

az functionapp create \
  --resource-group <RESOURCE_GROUP_NAME> \
  --consumption-plan-location <LOCATION> \
  --runtime dotnet \
  --functions-version 4 \
  --name <FUNCTION_APP_NAME> \
  --storage-account <STORAGE_ACCOUNT_NAME>

az functionapp identity assign \
  --name <FUNCTION_APP_NAME> \
  --resource-group <RESOURCE_GROUP_NAME>

az monitor app-insights component create \
  --app <APP_INSIGHTS_NAME> \
  --location <LOCATION> \
  --resource-group <RESOURCE_GROUP_NAME> \
  --application-type web
```

### Step 3: Assign RBAC Roles

#### **Azure Portal**

1. Go to *Function App* → *Identity* and confirm the Managed Identity.
2. Navigate to *Storage Account* → *Access Control (IAM)*.
3. Click *Add role assignment* and assign:
    - Storage Blob Data Contributor
    - Storage Queue Data Contributor
    - Storage Table Data Contributor
4. In *Members*, select *Managed Identity* linked to your Function App.
5. Scope: *This resource (the storage account)*.
6. Save and wait for RBAC propagation.

#### **Azure CLI**
Replace placeholders and run:

```Shell
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
```

### Step 4: Deploy ZIP

#### Azure Portal: 
Go to Function App → Deployment Center → Zip Deploy and upload function.zip.

#### Azure CLI:
```Shell
az functionapp deployment source config-zip \
  --resource-group <RESOURCE_GROUP_NAME> \
  --name <FUNCTION_APP_NAME> \
  --src function.zip
```

---

## 🔒 Advanced Security: Identity-Based Access + Network Isolation

To secure your Azure Function beyond basic authentication, apply **two complementary layers**:


### 1. Enforce Identity-Based Access with EasyAuth + App Roles

**EasyAuth (App Service Authentication)**:
- Built-in feature that intercepts all HTTP requests.
- Requires users to authenticate via Azure AD before reaching your function.
- No custom code needed for authentication.

**Configuration Steps**:
1. Go to **Function App → Authentication**.
2. Enable **App Service Authentication**.
3. Add **Microsoft Identity Provider**:
   - Choose **Current tenant**.
   - Configure **App Registration** (create new or use existing).

4. Under **Authentication settings**:
   - **Restrict access**: Require authentication.
   - **Unauthenticated requests**:
     - For APIs → **HTTP 401 Unauthorized**.
     - For browser-based → **Return HTTP 302 Found (Redirect)**.
5. Save changes.

**App Roles for Fine-Grained Authorization**:
- In **Azure AD → App registrations → [Your App] → App roles**, create roles like:
  - `FunctionExecutor`
  - `FunctionAdmin`
- Assign roles to users/groups via **Enterprise Applications → Users and Groups**.
- EasyAuth injects roles in the `X-MS-CLIENT-PRINCIPAL` header and JWT token.
- You can decode this header in your function to enforce roles if needed.


### 2. Apply Network Isolation with Access Restrictions or Private Endpoints

Even with authentication, your function is publicly accessible unless you restrict network access.

**Option A: Access Restrictions**
- Go to **Function App → Networking → Access Restrictions**.
- Add rules to:
  - Allow only corporate IP ranges.
  - Block all other traffic.
- Simple and effective for basic isolation.

**Option B: Private Endpoints (Recommended for Production)**
- Integrate your Function App with **Azure Private Link**:
  - Creates a private IP in your VNET.
  - Disables public internet access.
- Steps:
  1. Go to **Function App → Networking → Private Endpoint Connections**.
  2. Click **+ Add** and select your VNET and subnet.
  3. Configure DNS for private endpoint resolution.
- Requires VNET integration for clients or VPN/ExpressRoute connectivity.


### Best Practice for Production
- Combine **EasyAuth + App Roles** for identity-based access control.
- Add **Access Restrictions** or **Private Endpoints** for network-level isolation.
- Use **Conditional Access** in Azure AD for MFA and compliance.
- Disable public access to linked Storage Accounts and use private endpoints.


**Result**:  
Your Azure Function is protected by:
- **Authentication & Authorization** (EasyAuth + App Roles).
- **Network Isolation** (Access Restrictions or Private Endpoints).
- **RBAC for Management** (Azure resource-level control).


#### **Additional Hardening**
- Enable *HTTPS Only*.
- Secure linked *Storage Account* (disable public access, use private endpoints).

---

### Summary Checklist
- Build → Publish → Zip
- Create Function App with Storage Account and Managed Identity
- Assign RBAC roles for Durable Functions
- Deploy ZIP via Deployment Center or CLI
- Enable EasyAuth for Azure AD authentication
- Assign App Roles for fine-grained authorization
- Apply security hardening
