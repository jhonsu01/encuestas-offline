package com.encuestas.offline.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(
    entities = [SurveyorEntity::class, SurveyEntity::class, ResponseEntity::class],
    version = 1,
    exportSchema = false
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun surveyorDao(): SurveyorDao
    abstract fun surveyDao(): SurveyDao
    abstract fun responseDao(): ResponseDao

    companion object {
        @Volatile
        private var INSTANCE: AppDatabase? = null

        fun get(context: Context): AppDatabase =
            INSTANCE ?: synchronized(this) {
                INSTANCE ?: Room.databaseBuilder(
                    context.applicationContext,
                    AppDatabase::class.java,
                    "encuestas.db"
                ).fallbackToDestructiveMigration().build().also { INSTANCE = it }
            }
    }
}
