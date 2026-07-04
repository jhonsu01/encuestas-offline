package com.encuestas.offline.data.remote

import com.encuestas.offline.domain.Survey
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

interface EncuestasApi {
    @GET("health")
    suspend fun health(): HealthResponse

    @GET("surveys")
    suspend fun surveys(): List<Survey>

    @POST("auth")
    suspend fun auth(@Body req: AuthRequest): AuthResponse

    @POST("sync")
    suspend fun sync(@Body batch: SyncBatch): SyncResult
}

object ApiFactory {
    fun create(baseUrl: String): EncuestasApi {
        val normalized = if (baseUrl.endsWith("/")) baseUrl else "$baseUrl/"
        return Retrofit.Builder()
            .baseUrl(normalized)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(EncuestasApi::class.java)
    }
}
