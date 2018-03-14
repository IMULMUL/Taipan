// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/FSharpLog/lib/ES.FsLog.dll"

open System
open System.Collections.Generic
open System.IO

open Fake
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
 
// The name of the project
let project = "Taipan"

// Short summary of the project
let summary = "A web application vulnerability assessment tool."

// Longer description of the project
let description = "A web application vulnerability assessment tool."

// List of author names (for NuGet package)
let authors = [ "Enkomio" ]

// File system information
let solutionFile  = "TaipanSln.sln"

let appConfig = """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6.1" />
  </startup>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.4.0.0" newVersion="4.4.0.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>"""

// Build dir
let buildDir = "./build"

// Package dir
let deployDir = "./deploy"

// set the script dir as current
Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)

// Read additional information from the release notes document
let releaseNotesData = 
    let changelogFile = Path.Combine("..", "RELEASE_NOTES.md")
    File.ReadAllLines(changelogFile)
    |> parseAllReleaseNotes

let releaseNoteVersion = Version.Parse((List.head releaseNotesData).AssemblyVersion)
let buildVersion = int32(DateTime.UtcNow.Subtract(new DateTime(1980,2,1,0,0,0)).TotalSeconds)
let releaseVersionOfficial = new Version(releaseNoteVersion.Major, releaseNoteVersion.Minor, buildVersion)
let releaseVersion = {List.head releaseNotesData with AssemblyVersion = releaseVersionOfficial.ToString()}
trace("Version: " + releaseVersion.AssemblyVersion)

let stable = 
    match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
    | Some stable -> stable
    | _ -> releaseVersion

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath))
    let fileName = folderName @@ "AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Company (authors |> String.concat ", ")
        Attribute.Description summary
        Attribute.Version (releaseVersion.AssemblyVersion + ".*")
        Attribute.FileVersion (releaseVersion.AssemblyVersion + ".*")
        Attribute.InformationalVersion (releaseVersion.NugetVersion + ".*") ]

Target "Clean" (fun _ ->
    CleanDir buildDir
    ensureDirectory buildDir

    CleanDir deployDir
    ensureDirectory deployDir
)

Target "AssemblyInfo" (fun _ ->
  let fsProjs =  !! "*/**/*.fsproj"
  fsProjs |> Seq.iter genFSAssemblyInfo
)

Target "Compile" (fun _ ->
    // compile Taipan
    let projectName = "Taipan"
    let project = Path.Combine(projectName, projectName + ".fsproj")
    let fileName = Path.GetFileNameWithoutExtension(projectName)
    let buildAppDir = Path.Combine(buildDir, fileName)
    ensureDirectory buildAppDir
    MSBuildRelease buildAppDir "Build" [project] |> Log "Taipan Build Output: "
)

Target "CopyBinaries" (fun _ ->
    // copy chrome
    ensureDirectory (buildDir + "/Taipan/ChromeBins/Windows")    
    Unzip  (buildDir + "/Taipan/ChromeBins/Windows") ("../Bins/chrome-win32.zip")
        
    ensureDirectory (buildDir + "/Taipan/ChromeBins/Unix32")
    Unzip  (buildDir + "/Taipan/ChromeBins/Unix32") ("../Bins/chrome-linux32.zip")

    ensureDirectory (buildDir + "/Taipan/ChromeBins/Unix64")
    Unzip  (buildDir + "/Taipan/ChromeBins/Unix64") ("../Bins/chrome-linux64.zip")

    // copy ChromeDriver and clean build
    ensureDirectory (buildDir + "/Taipan/driver")
    FileUtils.rm (buildDir + "/Taipan/chromedriver")
    FileUtils.rm (buildDir + "/Taipan/chromedriver.exe")
    FileUtils.cp_r "../Bins/driver/" (buildDir + "/Taipan/driver")    
)

// Generate assembly info files with the right version & up-to-date information
Target "Release" (fun _ ->
    let forbidden = [".pdb"]
    !! (buildDir + "/Taipan/**/*.*")         
    |> Seq.filter(fun f -> 
        forbidden 
        |> List.contains (Path.GetExtension(f).ToLowerInvariant())
        |> not
    )
    |> Zip buildDir (Path.Combine(deployDir, "Taipan." + releaseVersion.AssemblyVersion + ".zip"))
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Compile"
  //==> "CopyBinaries"
  //==> "Release"
  ==> "All"

RunTargetOrDefault "All"