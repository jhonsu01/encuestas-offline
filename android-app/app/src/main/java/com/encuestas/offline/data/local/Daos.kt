package com.encuestas.offline.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface SurveyorDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(s: SurveyorEntity)

    @Query("SELECT * FROM surveyors LIMIT 1")
    suspend fun getCurrent(): SurveyorEntity?
}

@Dao
interface SurveyDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(list: List<SurveyEntity>)

    @Query("SELECT * FROM surveys")
    suspend fun getAll(): List<SurveyEntity>
}

@Dao
interface ResponseDao {
    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(r: ResponseEntity): Long

    @Query("SELECT * FROM responses WHERE synced = 0")
    suspend fun getPending(): List<ResponseEntity>

    @Query("UPDATE responses SET synced = 1, batchPin = :pin WHERE signature IN (:sigs)")
    suspend fun markSynced(sigs: List<String>, pin: String)

    @Query("SELECT COUNT(*) FROM responses")
    suspend fun count(): Int

    @Query("SELECT COUNT(*) FROM responses WHERE synced = 0")
    suspend fun countPending(): Int
}
