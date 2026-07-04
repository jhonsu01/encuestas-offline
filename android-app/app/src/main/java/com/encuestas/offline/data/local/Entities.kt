package com.encuestas.offline.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "surveyors")
data class SurveyorEntity(
    @PrimaryKey val id: String,
    val documentType: String,
    val documentNumber: String,
    val fullName: String,
    val pin: String
)

@Entity(tableName = "surveys")
data class SurveyEntity(
    @PrimaryKey val id: String,
    val version: Int,
    val title: String,
    val json: String
)

@Entity(tableName = "responses")
data class ResponseEntity(
    @PrimaryKey val signature: String,
    val surveyId: String,
    val surveyorId: String,
    val timestamp: String,
    val answersJson: String,
    val latitude: Double?,
    val longitude: Double?,
    val imagePath: String?,
    val batchPin: String?,
    val synced: Boolean = false
)
