plugins {
    alias(libs.plugins.android.library)
}
android {
    namespace = "com.example.googleads"
    compileSdk = 34

    defaultConfig {
        minSdk = 24
        // Required if your library + dependencies exceed the 64k method limit
        multiDexEnabled = true
    }

    compileOptions {
        // This MUST be inside compileOptions

        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }


}
dependencies {
    // 1. Force alignment using constraints
    constraints {
        implementation("org.jetbrains.kotlin:kotlin-stdlib-jdk7:1.8.10") {
            because("kotlin-stdlib-jdk7 is now part of kotlin-stdlib 1.8.10")
        }
        implementation("org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.8.10") {
            because("kotlin-stdlib-jdk8 is now part of kotlin-stdlib 1.8.10")
        }
    }

    // 2. Your existing dependencies
    implementation("com.google.ads.interactivemedia.v3:interactivemedia:3.31.0")
    // Local Unit Tests (Run on your computer's JVM)
    testImplementation("junit:junit:4.13.2")
    // Instrumented Tests (Run on a physical device or emulator)
    androidTestImplementation("androidx.test.ext:junit:1.1.5")
}

// 3. The "Hammer": Force everything to 1.8.10
configurations.all {
    resolutionStrategy {
        force("org.jetbrains.kotlin:kotlin-stdlib:1.8.10")
        force("org.jetbrains.kotlin:kotlin-stdlib-jdk7:1.8.10")
        force("org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.8.10")
    }
}