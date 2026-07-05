package com.encuestas.offline.data.remote

import com.encuestas.offline.domain.Survey

data class HealthResponse(
    val status: String = "",
    val service: String? = null,
    val version: String? = null,
    val time: String? = null
)

data class AuthRequest(val pin: String, val deviceId: String)
data class AuthResponse(val token: String = "", val expiresIn: Int = 0)

data class ResponseDto(
    val surveyId: String,
    val surveyorId: String,
    val timestamp: String,
    val answers: Map<String, String>,
    val latitude: Double?,
    val longitude: Double?,
    val imageBase64: String?,
    val signature: String
)

data class SyncBatch(
    val pin: String,
    val deviceId: String,
    val surveyorDocument: String,
    val surveyorName: String,
    val responses: List<ResponseDto>
)

data class SyncResult(
    val accepted: Int = 0,
    val duplicates: Int = 0,
    val rejected: Int = 0,
    val batchId: String? = null
)

/** Lista de encuestas devuelta por GET /surveys. */
typealias SurveyList = List<Survey>
