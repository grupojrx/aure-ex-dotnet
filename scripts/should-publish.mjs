/**
 * Decide se AureEx (NuGet) deve publicar.
 * exit 0 = publish, 10 = skip, 1 = erro
 *
 * nupkg é binário (sem .cs) — o gate usa presença da versão no NuGet.
 * Mudança de código exige bump de Version em AureEx.csproj (sdk:generate / manual).
 */
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const packageId = 'AureEx'
const userAgent = 'AureExSDKPublish (mailto=dev@aure-ex.com)'

function readLocalVersion() {
  const text = readFileSync(join(root, 'AureEx/AureEx.csproj'), 'utf8')
  const match = text.match(/<Version>([^<]+)<\/Version>/)

  if (!match) {
    throw new Error('Version missing in AureEx.csproj')
  }

  return match[1]
}

async function fetchNugetVersions() {
  const id = packageId.toLowerCase()
  const response = await fetch(`https://api.nuget.org/v3-flatcontainer/${id}/index.json`, {
    headers: { 'User-Agent': userAgent }
  })

  if (response.status === 404) {
    return []
  }

  if (!response.ok) {
    throw new Error(`NuGet HTTP ${response.status}`)
  }

  const data = await response.json()

  return data.versions || []
}

if (!existsSync(join(root, 'AureEx/AureEx.cs'))) {
  throw new Error('AureEx/AureEx.cs missing')
}

const version = readLocalVersion()
const versions = await fetchNugetVersions()

if (versions.length === 0) {
  console.log(`no remote package — publish ${packageId} ${version}`)
  process.exit(0)
}

if (versions.includes(version)) {
  console.log(`${packageId} ${version} already on NuGet — skip`)
  process.exit(10)
}

console.log(`${packageId} ${version} not on NuGet — publish`)
process.exit(0)
