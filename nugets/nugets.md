# NuGet CI/CD For DrawnUI

This repo has a GitHub action:

- `.github/workflows/nuget-release.yml`

## What The Workflow Does

The workflow is manual and runs from the GitHub Actions UI with `workflow_dispatch`.

It has three jobs in order:

1. `Pack NuGet packages`
2. `Publish to NuGet.org`
3. `Publish to GitHub Packages`

The second and third jobs use the artifact produced by the first job, so the exact packages that were packed are the ones that get published.

## Package List

The workflow packs the same projects that are currently packed by `makenugets.bat`, except it removes the duplicate Camera pack invocation from the batch script and packs each project once:

- `src/Maui/DrawnUi/DrawnUi.Maui.csproj`
- `src/Maui/MetaPackage/AppoMobi.Maui.DrawnUi/AppoMobi.Maui.DrawnUi.csproj`
- `src/Maui/Addons/DrawnUi.Maui.Game/DrawnUi.Maui.Game.csproj`
- `src/Maui/Addons/DrawnUi.Maui.MapsUi/DrawnUi.Maui.MapsUi.csproj`
- `src/Maui/Addons/DrawnUi.MauiGraphics/DrawnUi.MauiGraphics.csproj`

Package versions come from the repository build metadata, currently centralized in `src/Directory.Build.targets`.

## Produced Artifacts

The pack job collects these files from the `bin/Release` folders under `src/Maui`:

- `*.nupkg`
- `*.snupkg`

Those files are uploaded as a workflow artifact named `nuget-packages`.

This replaces what `movenugets.bat` used to do with `C:\Nugets`.

## Required Repository Secrets

Create this repository secret before running the full publish flow:

- `NUGET_API_KEY`: required for publishing to NuGet.org

GitHub Packages publishing does not require a custom repository secret in the current workflow. 
It uses the built-in `GITHUB_TOKEN` issued by GitHub Actions for the repository that runs the workflow.

## GitHub Settings To Verify

Before first use, verify these repository settings:

1. GitHub Actions is enabled for the repository.
2. The repository allows manual workflow runs.
3. The `GITHUB_TOKEN` can write packages for this repository.
4. The repository owner matches the package namespace you expect for GitHub Packages.

The GitHub Packages feed URL used by the workflow is:

- `https://nuget.pkg.github.com/<repository-owner>/index.json`

For this repository, the owner is expected to come from `${{ github.repository_owner }}` at runtime.

## How To Add The Required Secret

In GitHub:

1. Open the repository.
2. Go to `Settings`.
3. Open `Secrets and variables`.
4. Open `Actions`.
5. Add a new repository secret named `NUGET_API_KEY`.
6. Paste the API key from NuGet.org.

## How To Run The Workflow

1. Open the repository `Actions` tab.
2. Select the workflow `nuget release`.
3. Click `Run workflow`.
4. Choose whether to publish to NuGet.org.
5. Choose whether to publish to GitHub Packages.
6. Start the run.

Recommended first test:

1. Run with `publish_nuget = false`
2. Run with `publish_github = false`

That validates packing and artifact collection without publishing anything.

## Job Behavior Details

### Job 1: Pack NuGet packages

This job:

- checks out the repository
- installs the SDK from `global.json`
- installs the `maui` workload
- runs `dotnet pack` for each package project in `Release`
- collects all `.nupkg` and `.snupkg` files into one artifact

### Job 2: Publish to NuGet.org

This job:

- downloads the `nuget-packages` artifact
- requires `NUGET_API_KEY`
- pushes every `.nupkg` with `-SkipDuplicate`

Important detail:

- the artifact also contains matching `.snupkg` files
- `nuget push` against the NuGet V3 endpoint can publish the symbol package alongside the main package when the pair is present

This is why the workflow keeps `.snupkg` files in the artifact even though the publish loop targets `.nupkg` files.

### Job 3: Publish to GitHub Packages

This job:

- waits for the pack job
- also waits for the NuGet.org job if that job was enabled
- skips GitHub publishing if the NuGet.org job failed
- downloads the same artifact
- authenticates to the GitHub Packages NuGet feed using `GITHUB_TOKEN`
- pushes `.nupkg` files with `--skip-duplicate`

Important detail:

- this job intentionally pushes only `.nupkg`
- it does not push `.snupkg` to GitHub Packages
- symbol packages remain useful for NuGet.org and for archived build artifacts

### Pack Job Fails

Check:

- SDK version from `global.json`
- MAUI workload installation logs
- whether any project path in the workflow no longer exists
- whether a new package project was added and needs to be included in the project list

### NuGet.org Publish Fails

Check:

- `NUGET_API_KEY` exists and is valid
- the package version has not already been published without `SkipDuplicate` handling being enough for your scenario
- NuGet.org service status if validation appears delayed

If duplicates are the only issue, the workflow should continue because `-SkipDuplicate` is enabled.

### GitHub Packages Publish Fails

Check:

- the package namespace owner is correct
- the workflow token has package write access
- the repository URL embedded in package metadata points to the expected repository owner
- package visibility and permissions in GitHub Packages if this is not the first publish

### Some Packages Are Missing From The Artifact

Check:

- whether the project was actually packed
- whether its output still goes under a `bin/Release` path below `src/Maui`
- whether the workflow project list includes it

## Maintenance Notes

If you add or remove a package project later, update the project array in `.github/workflows/nuget-release.yml`.

If package versioning changes later, the workflow should not need filename updates because it discovers produced packages dynamically.

If you ever decide to publish only selected packages, the clean place to change that behavior is the pack job project list or the publish step filters, not by reintroducing hardcoded version masks.

## Suggested First Validation Sequence

1. Run the workflow with both publish toggles disabled.
2. Inspect the `nuget-packages` artifact and confirm all expected packages are present.
3. Run again with `publish_nuget = true` and `publish_github = false`.
4. Confirm NuGet.org packages and symbol packages look correct.
5. Run again with `publish_github = true`.
6. Confirm packages appear under GitHub Packages for the repository owner namespace.

After that is verified end to end, this repo state is a good basis for extracting a dedicated MAUI NuGet CI/CD skill.