# IMA SDK – Unity Android Integration

A pre-configured `.aar` bundle and Gradle setup for integrating **Google IMA SDK** into Unity Android builds.

---

## 📦 Installation

### Step 1 – Download the `.aar`

Go to the [Releases](../../releases) section of this repository and download the latest `.aar` file.

### Step 2 – Place the `.aar` in your Unity project

```
Assets/Plugins/Android/
```

---

## ⚙️ Gradle Configuration

Two Gradle template files need to be updated. In Unity, you can find these under:

**Edit → Project Settings → Player → Publishing Settings → Custom Gradle Template**

---

### `mainTemplate.gradle`

Make the following 4 changes:

**1. Add `allprojects` block above the `dependencies` section**

**2. Add IMA SDK and related dependencies inside `dependencies {}`**

**3. Add `coreLibraryDesugaringEnabled true` inside `compileOptions {}`**

**4. Add the `configurations.all` force-resolution block at the end of the file**

<details>
<summary>📄 View complete <code>mainTemplate.gradle</code> sample</summary>

```groovy
apply plugin: 'com.android.library'
apply from: '../shared/keepUnitySymbols.gradle'
apply from: '../shared/common.gradle'
**APPLY_PLUGINS**


allprojects {
    configurations.all {
        resolutionStrategy.eachDependency { DependencyResolveDetails details ->
            if (details.requested.group == 'org.jetbrains.kotlin' &&
               (details.requested.name == 'kotlin-stdlib-jdk7' || details.requested.name == 'kotlin-stdlib-jdk8')) {
                details.useTarget "org.jetbrains.kotlin:kotlin-stdlib:1.8.10"
            }
        }
    }
}

dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])

    implementation ("com.google.ads.interactivemedia.v3:interactivemedia:3.39.0") {
        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'
        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'
    }
    implementation 'com.google.android.gms:play-services-ads-identifier:18.0.1'
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.3'
    implementation "org.jetbrains.kotlin:kotlin-stdlib:1.8.10"
**DEPS**}


android {
    namespace "com.unity3d.player"
    ndkPath "**NDKPATH**"
    ndkVersion "**NDKVERSION**"

    compileSdk **APIVERSION**
    buildToolsVersion = "**BUILDTOOLS**"

    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
        coreLibraryDesugaringEnabled true
    }

    defaultConfig {
        minSdk **MINSDK**
        targetSdk **TARGETSDK**
        ndk {
            abiFilters **ABIFILTERS**
            debugSymbolLevel **DEBUGSYMBOLLEVEL**
        }
        versionCode **VERSIONCODE**
        versionName '**VERSIONNAME**'
        consumerProguardFiles 'proguard-unity.txt'**USER_PROGUARD**
**DEFAULT_CONFIG_SETUP**
    }

    lint {
        abortOnError false
    }

    androidResources {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = "!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~"
    }**PACKAGING**
}
**IL_CPP_BUILD_SETUP**
**SOURCE_BUILD_SETUP**
**EXTERNAL_SOURCES**


configurations.all {
    resolutionStrategy {
        // Forces all transitive dependencies to use a unified Kotlin stdlib version
        force "org.jetbrains.kotlin:kotlin-stdlib:1.8.10"
        force "org.jetbrains.kotlin:kotlin-stdlib-jdk7:1.8.10"
        force "org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.8.10"
    }
}
```

</details>

---

### `launcherTemplate.gradle`

Make the following 2 changes:

**1. Add `coreLibraryDesugaring` to the `dependencies {}` block**

**2. Add `coreLibraryDesugaringEnabled true` inside `compileOptions {}`**

<details>
<summary>📄 View complete <code>launcherTemplate.gradle</code> sample</summary>

```groovy
apply plugin: 'com.android.application'
apply from: 'setupSymbols.gradle'
apply from: '../shared/keepUnitySymbols.gradle'
apply from: '../shared/common.gradle'

dependencies {
    implementation project(':unityLibrary')
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.3'
}

android {
    namespace "**NAMESPACE**"
    ndkPath "**NDKPATH**"
    ndkVersion "**NDKVERSION**"

    compileSdk **APIVERSION**
    buildToolsVersion = "**BUILDTOOLS**"

    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
        coreLibraryDesugaringEnabled true
    }

    defaultConfig {
        minSdk **MINSDK**
        targetSdk **TARGETSDK**
        applicationId '**APPLICATIONID**'
        ndk {
            abiFilters **ABIFILTERS**
            debugSymbolLevel **DEBUGSYMBOLLEVEL**
        }
        versionCode **VERSIONCODE**
        versionName '**VERSIONNAME**'
    }

    androidResources {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = "!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~"
    }**SIGN**

    lint {
        abortOnError false
    }

    buildTypes {
        debug {
            minifyEnabled **MINIFY_DEBUG**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
            jniDebuggable true
        }
        release {
            minifyEnabled **MINIFY_RELEASE**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
        }
    }**PACKAGING****PLAY_ASSET_PACKS****SPLITS**
**BUILT_APK_LOCATION**
    bundle {
        language { enableSplit = false }
        density  { enableSplit = false }
        abi      { enableSplit = true  }
        texture  { enableSplit = true  }
    }

    **GOOGLE_PLAY_DEPENDENCIES**
}**SPLITS_VERSION_CODE****LAUNCHER_SOURCE_BUILD_SETUP**
```

</details>

---

## 📋 Summary Checklist

| File | Change |
|------|--------|
| `Assets/Plugins/Android/` | Add the `.aar` file |
| `mainTemplate.gradle` | Add `allprojects { configurations.all { ... } }` block above `dependencies` |
| `mainTemplate.gradle` | Add IMA SDK + GMS + Kotlin + desugar dependencies |
| `mainTemplate.gradle` | Add `coreLibraryDesugaringEnabled true` in `compileOptions` |
| `mainTemplate.gradle` | Add `configurations.all { resolutionStrategy { force ... } }` at end of file |
| `launcherTemplate.gradle` | Add `coreLibraryDesugaring` dependency |
| `launcherTemplate.gradle` | Add `coreLibraryDesugaringEnabled true` in `compileOptions` |

---

## 🔗 Dependencies Used

| Dependency | Version |
|------------|---------|
| `com.google.ads.interactivemedia.v3:interactivemedia` | `3.39.0` |
| `com.google.android.gms:play-services-ads-identifier` | `18.0.1` |
| `com.android.tools:desugar_jdk_libs` | `2.1.3` |
| `org.jetbrains.kotlin:kotlin-stdlib` | `1.8.10` |
