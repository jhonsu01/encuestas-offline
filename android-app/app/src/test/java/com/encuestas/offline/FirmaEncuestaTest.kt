package com.encuestas.offline

import com.encuestas.offline.domain.FirmaEncuesta
import com.encuestas.offline.domain.HorarioValidator
import com.encuestas.offline.domain.Schedule
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalTime

class FirmaEncuestaTest {

    @Test
    fun serializeAnswers_ordenaPorClave() {
        val s = FirmaEncuesta.serializeAnswers(mapOf("b" to "2", "a" to "1"))
        assertEquals("a=1;b=2", s)
    }

    @Test
    fun firmar_esDeterministico() {
        val a = FirmaEncuesta.firmar("s1", "CC-1", "2026-07-04T00:00:00Z", mapOf("q1" to "x"))
        val b = FirmaEncuesta.firmar("s1", "CC-1", "2026-07-04T00:00:00Z", mapOf("q1" to "x"))
        assertEquals(a, b)
        assertEquals(64, a.length) // SHA-256 en hex
    }

    @Test
    fun firmar_conocido_coincideConCSharp() {
        // Mismo vector debe producir el mismo hash en C# (SignatureService).
        // payload = "s1" + "CC-1" + "2026-01-01T00:00:00Z" + "q1=hola"
        val hash = FirmaEncuesta.firmar("s1", "CC-1", "2026-01-01T00:00:00Z", mapOf("q1" to "hola"))
        assertEquals("91a136ffc85ac4b459ade86e2b1397b2a7e9998b3ec58ad781568d021eda5793", hash)
    }

    @Test
    fun horario_dentroYFuera() {
        val sched = Schedule(startTime = "06:00", endTime = "20:00")
        assertTrue(HorarioValidator.dentroDeHorario(sched, LocalTime.of(10, 0)))
        assertFalse(HorarioValidator.dentroDeHorario(sched, LocalTime.of(21, 0)))
    }

    @Test
    fun horario_cruzandoMedianoche() {
        val sched = Schedule(startTime = "20:00", endTime = "06:00")
        assertTrue(HorarioValidator.dentroDeHorario(sched, LocalTime.of(23, 0)))
        assertTrue(HorarioValidator.dentroDeHorario(sched, LocalTime.of(3, 0)))
        assertFalse(HorarioValidator.dentroDeHorario(sched, LocalTime.of(12, 0)))
    }
}
