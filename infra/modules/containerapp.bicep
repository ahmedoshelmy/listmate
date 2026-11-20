param name string
param location string
param environmentId string
param image string
param targetPort int
param external bool

param secrets array
param env array

@description('More than one replica needs the Redis backplane (enableRedis in main.bicep).')
param maxReplicas int = 1

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: external
        targetPort: targetPort
        transport: 'auto'
      }
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: name
          image: image
          env: env
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
        }
      ]
      scale: { minReplicas: 0, maxReplicas: maxReplicas }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
