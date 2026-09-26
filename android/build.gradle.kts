// Top-level build file. Plugin versions are pinned here and applied in :app.
// Alignment: Kotlin 2.0.x + Jetpack Compose + AGP 8.7 (bump compileSdk/targetSdk
// to 36 for Android 16 once the local SDK ships it — see spec §9).
plugins {
    id("com.android.application") version "8.7.3" apply false
    id("org.jetbrains.kotlin.android") version "2.0.21" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.0.21" apply false
}
