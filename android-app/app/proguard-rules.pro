# Gson / modelos serializados por reflexión
-keep class com.encuestas.offline.domain.** { *; }
-keep class com.encuestas.offline.data.remote.** { *; }
-keepattributes Signature
-keepattributes *Annotation*

# Retrofit
-dontwarn okhttp3.**
-dontwarn okio.**
-dontwarn retrofit2.**
