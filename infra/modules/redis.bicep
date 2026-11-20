param name string
param location string

// Basic C0: the smallest cache, enough for a SignalR backplane and a Data Protection key ring. Not free.
resource cache 'Microsoft.Cache/redis@2024-03-01' = {
  name: name
  location: location
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

#disable-next-line outputs-should-not-contain-secrets
output connectionString string = '${cache.properties.hostName}:6380,password=${cache.listKeys().primaryKey},ssl=True,abortConnect=False'
