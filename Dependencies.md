# Dependencies

## Third-Party Packages

Every NuGet package is code from someone else that runs on your machine when you build, or in Oraculum when it runs. So each one is checked before it's added, pinned to an exact version, and kept to the projects that need it, and every restore checks the packages again.

### Safeguards

These apply to every restore and build of Oraculum and its tests:

| Safeguard | Where | What it does |
|---|---|---|
| Exact versions | `Directory.Packages.props` | Every package's version is set in one place and pinned with `[x.y.z]`, so no other version can be used. A project that needs a different version says so with `VersionOverride`, also pinned. |
| Lock files | `Directory.Build.props`, each project's `packages.lock.json` | Each project records the version and content hash of every package it uses, directly or not. Restores run in locked mode: they fail if a package's contents differ from the hash, or if a version would change. |
| One source | `nuget.config` | Packages only come from nuget.org, whatever sources the machine lists, and package source mapping stops a package of the same name on another source from being used. |
| Signatures | `nuget.config` | Every package must be signed by a trusted signer: nuget.org's repository signature, by its signing certificates' fingerprints. |
| Vulnerability audit | `Directory.Build.props` | Restores check every package, direct or not, against nuget.org's list of known vulnerabilities, of any severity. A vulnerable package stops the build (NU1901 to NU1904 are errors). A restore that can't reach nuget.org to check only warns (NU1900). |
| Limited reach | The project files | A package is referenced only by the project that needs it. Analyzers are `PrivateAssets="all"`, so they don't pass on to projects that reference the one using them. |

The solution also builds the GoldenAnvil.Utility projects it references. This repository's `nuget.config` applies to them too when this solution is restored, while their versions and settings come from GoldenAnvil.Utility's own `Directory.Build.props` and `Directory.Packages.props`, which have the same safeguards.

Oraculum's lock files record what the GoldenAnvil.Utility projects ask for. When a package changes there, Oraculum's lock files need updating too (step 4 below), or its restores fail in locked mode.

### Adding or updating a package

1. **Choose a version** that has been out for a few months, with many downloads, and that isn't marked deprecated or vulnerable on nuget.org. A brand-new release is the most likely to be a compromised one.
2. **Inspect it before it's restored.** Download the exact package into an empty scratch folder (from `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg`, with the id in lower case) and list its contents (it's a zip). It should hold only `lib/` assemblies (and their documentation), with no `build/`, `buildTransitive/`, `analyzers/`, `tools/`, or `content/` folders, which run or add code during a build, unless that's what the package is for (such as a test adapter or an analyzer). Read its `.nuspec` for its dependencies (each one needs the same checks) and license, and run `dotnet nuget verify <file> --all -v n` to check its signature and see its owners.
3. **Add it** to `Directory.Packages.props` with an exact version (`[x.y.z]`), and reference it only from the projects that need it.
4. **Update the lock files**, which locked mode otherwise refuses:

   ```bash
   dotnet restore Oraculum.slnx -p:RestoreLockedMode=false --force-evaluate
   ```

   Check that the new `contentHash` in the lock file is the one `dotnet nuget verify` showed.

When nuget.org starts signing with a new certificate, restores fail with a signature error until it's trusted. Check the certificate on nuget.org, then add it:

```bash
dotnet nuget trust source nuget.org --configfile nuget.config
```

When the audit finds a vulnerability, update the package (or the package that brings it in) to a version that fixes it, rather than turning the audit off.

Packages already in the local cache (`%USERPROFILE%\.nuget\packages`) aren't checked again on restore, since they're only checked when they're downloaded; the lock files' content hashes still catch any that change. Some very old packages in the cache were downloaded before nuget.org signed every package, so `dotnet nuget verify` reports them as unsigned (NU3004) even though nuget.org's copies are signed with the same contents.
