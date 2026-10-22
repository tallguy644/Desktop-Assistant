plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "com.tallguy644.syracompanion"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.tallguy644.syracompanion"
        minSdk = 26
        targetSdk = 35
        versionCode = 6
        versionName = "1.5"
    }

    // A fixed key (supplied by CI from repository secrets) lets each new build install over the last one.
    // Without it, Gradle's per-machine debug key is used and updates need an uninstall first.
    val keystorePath = System.getenv("SYRA_KEYSTORE")
    signingConfigs {
        if (keystorePath != null) {
            create("syra") {
                storeFile = file(keystorePath)
                storePassword = System.getenv("SYRA_KEYSTORE_PASSWORD")
                keyAlias = "syra"
                keyPassword = System.getenv("SYRA_KEYSTORE_PASSWORD")
            }
        }
    }
    buildTypes {
        debug {
            if (keystorePath != null) signingConfig = signingConfigs.getByName("syra")
        }
        release {
            isMinifyEnabled = false
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    buildFeatures {
        compose = true
        buildConfig = true
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-core")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
    debugImplementation("androidx.compose.ui:ui-tooling")
}
