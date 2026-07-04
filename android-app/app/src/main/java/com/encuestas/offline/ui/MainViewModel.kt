package com.encuestas.offline.ui

import android.annotation.SuppressLint
import android.app.Application
import android.provider.Settings
import android.util.Base64
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.encuestas.offline.data.discovery.DiscoveryClient
import com.encuestas.offline.data.discovery.ServerInfo
import com.encuestas.offline.data.local.AppDatabase
import com.encuestas.offline.data.local.ResponseEntity
import com.encuestas.offline.data.local.SurveyEntity
import com.encuestas.offline.data.local.SurveyorEntity
import com.encuestas.offline.data.remote.ApiFactory
import com.encuestas.offline.data.remote.ResponseDto
import com.encuestas.offline.data.remote.SyncBatch
import com.encuestas.offline.domain.DocumentType
import com.encuestas.offline.domain.FirmaEncuesta
import com.encuestas.offline.domain.FormValidator
import com.encuestas.offline.domain.HorarioValidator
import com.encuestas.offline.domain.Survey
import com.encuestas.offline.domain.Surveyor
import com.google.android.gms.location.LocationServices
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.tasks.await
import kotlinx.coroutines.withContext
import java.io.File
import java.time.Instant
import kotlin.random.Random

enum class Screen { REGISTRO, LOGIN, HOME, FORM, CAMARA, CIERRE }

class MainViewModel(app: Application) : AndroidViewModel(app) {

    private val db = AppDatabase.get(app)
    private val gson = Gson()

    @SuppressLint("HardwareIds")
    private val deviceId: String =
        "android-" + (Settings.Secure.getString(app.contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown")

    var screen by mutableStateOf(Screen.REGISTRO); private set
    var surveyor by mutableStateOf<Surveyor?>(null); private set
    val surveys = mutableStateListOf<Survey>()
    var currentSurvey by mutableStateOf<Survey?>(null); private set
    val answers = mutableStateMapOf<String, String>()
    var imagePath by mutableStateOf<String?>(null); private set
    var lat by mutableStateOf<Double?>(null); private set
    var lon by mutableStateOf<Double?>(null); private set
    var server by mutableStateOf<ServerInfo?>(null); private set
    var status by mutableStateOf("Listo"); private set
    var totalCount by mutableStateOf(0); private set
    var pendingCount by mutableStateOf(0); private set
    var syncPin by mutableStateOf<String?>(null); private set
    var busy by mutableStateOf(false); private set

    init {
        viewModelScope.launch {
            db.surveyorDao().getCurrent()?.let {
                surveyor = Surveyor(
                    DocumentType.valueOf(it.documentType),
                    it.documentNumber, it.fullName, it.pin
                )
                // Recordamos al encuestador, pero pedimos el PIN para entrar.
                screen = Screen.LOGIN
            }
            loadLocalSurveys()
            refreshCounts()
        }
    }

    fun navigate(s: Screen) { screen = s }

    /** Ingreso con PIN (el encuestador lo memoriza; nunca se autocompleta). */
    fun login(pin: String) {
        val sv = surveyor ?: run { navigate(Screen.REGISTRO); return }
        if (pin == sv.pin) {
            status = "Bienvenido, ${sv.fullName}"
            navigate(Screen.HOME)
        } else {
            status = "PIN incorrecto"
        }
    }

    /** Cierra la sesión: conserva los datos del encuestador y vuelve a pedir el PIN. */
    fun logout() {
        answers.clear(); imagePath = null; lat = null; lon = null; currentSurvey = null
        status = "Sesión cerrada"
        navigate(Screen.LOGIN)
    }

    /** Registrar un encuestador distinto (sobrescribe el actual). */
    fun cambiarEncuestador() {
        status = "Registra un nuevo encuestador"
        navigate(Screen.REGISTRO)
    }

    fun registrar(type: DocumentType, doc: String, name: String, pin: String) {
        if (doc.isBlank() || name.isBlank() || pin.length !in 4..8) {
            status = "Completa documento, nombre y un PIN de 4 a 8 dígitos"
            return
        }
        val sv = Surveyor(type, doc.trim(), name.trim(), pin.trim())
        viewModelScope.launch {
            db.surveyorDao().upsert(
                SurveyorEntity(sv.id, sv.documentType.name, sv.documentNumber, sv.fullName, sv.pin)
            )
            surveyor = sv
            status = "Encuestador registrado: ${sv.fullName}"
            navigate(Screen.HOME)
        }
    }

    fun abrirEncuesta(s: Survey) {
        if (!HorarioValidator.dentroDeHorario(s.schedule)) {
            status = "Fuera del horario permitido (${s.schedule?.startTime}–${s.schedule?.endTime})"
            return
        }
        answers.clear(); imagePath = null; lat = null; lon = null
        currentSurvey = s
        status = "Diligenciando: ${s.title}"
        navigate(Screen.FORM)
    }

    fun setAnswer(qid: String, value: String) { answers[qid] = value }

    fun toggleMultiple(qid: String, option: String) {
        val current = answers[qid]?.split(",")?.filter { it.isNotBlank() }?.toMutableSet() ?: mutableSetOf()
        if (!current.add(option)) current.remove(option)
        answers[qid] = current.sorted().joinToString(",")
    }

    fun onImageCaptured(path: String) {
        imagePath = path
        status = "Imagen capturada"
        navigate(Screen.FORM)
    }

    @SuppressLint("MissingPermission")
    fun capturarGps() {
        viewModelScope.launch {
            try {
                val client = LocationServices.getFusedLocationProviderClient(getApplication<Application>())
                val loc = client.lastLocation.await()
                if (loc != null) {
                    lat = loc.latitude; lon = loc.longitude
                    status = "GPS: %.5f, %.5f".format(loc.latitude, loc.longitude)
                } else {
                    status = "Sin ubicación. Activa el GPS y reintenta."
                }
            } catch (e: Exception) {
                status = "Error GPS: ${e.message}"
            }
        }
    }

    fun guardarRespuesta() {
        val s = currentSurvey ?: return
        val sv = surveyor ?: return
        val errors = FormValidator.validar(
            s.questions, answers, imagePath != null, lat != null && lon != null
        )
        if (errors.isNotEmpty()) { status = errors.first(); return }

        val ts = Instant.now().toString()
        val answersMap = answers.toMap()
        val sig = FirmaEncuesta.firmar(s.id, sv.id, ts, answersMap)
        val entity = ResponseEntity(
            signature = sig, surveyId = s.id, surveyorId = sv.id, timestamp = ts,
            answersJson = gson.toJson(answersMap), latitude = lat, longitude = lon,
            imagePath = imagePath, batchPin = null, synced = false
        )
        viewModelScope.launch {
            db.responseDao().insert(entity)
            refreshCounts()
            answers.clear(); imagePath = null; lat = null; lon = null; currentSurvey = null
            status = "Respuesta guardada y firmada (${sig.take(8)}…)"
            navigate(Screen.HOME)
        }
    }

    fun buscarServidor() {
        viewModelScope.launch {
            busy = true
            status = "Buscando servidor en la red…"
            val found = DiscoveryClient(getApplication()).discover()
            server = found
            status = if (found != null) "Servidor: ${found.host}:${found.port} (${found.method})"
            else "No se encontró servidor. Ingresa la IP manualmente."
            busy = false
        }
    }

    fun setServidorManual(host: String, port: Int) {
        if (host.isBlank()) { status = "IP inválida"; return }
        server = ServerInfo(host.trim(), port, "manual")
        status = "Servidor manual: ${host.trim()}:$port"
    }

    fun descargarEncuestas() {
        val srv = server ?: run { status = "Primero busca o define el servidor"; return }
        viewModelScope.launch {
            busy = true
            try {
                val api = ApiFactory.create("http://${srv.host}:${srv.port}")
                val list = withContext(Dispatchers.IO) { api.surveys() }
                db.surveyDao().upsertAll(list.map { SurveyEntity(it.id, it.version, it.title, gson.toJson(it)) })
                surveys.clear(); surveys.addAll(list)
                status = "Descargadas ${list.size} encuestas"
            } catch (e: Exception) {
                status = "Error al descargar: ${e.message}"
            }
            busy = false
        }
    }

    fun cerrarDiaYSincronizar(pinLocal: String) {
        val sv = surveyor ?: return
        if (pinLocal != sv.pin) { status = "PIN local incorrecto"; return }
        val pin = (100000 + Random.nextInt(900000)).toString() // PIN de sync de 6 dígitos
        syncPin = pin
        sincronizar(pin)
    }

    private fun sincronizar(pin: String) {
        val srv = server ?: run { status = "No hay servidor. Búscalo primero."; return }
        val sv = surveyor ?: return
        viewModelScope.launch {
            busy = true
            try {
                val api = ApiFactory.create("http://${srv.host}:${srv.port}")
                val pending = db.responseDao().getPending()
                if (pending.isEmpty()) { status = "No hay datos pendientes de sincronizar"; busy = false; return@launch }
                val dtos = pending.map { r ->
                    val img = r.imagePath?.let { p ->
                        val f = File(p)
                        if (f.exists()) Base64.encodeToString(f.readBytes(), Base64.NO_WRAP) else null
                    }
                    val ans: Map<String, String> =
                        gson.fromJson(r.answersJson, object : TypeToken<Map<String, String>>() {}.type)
                    ResponseDto(
                        surveyId = r.surveyId, surveyorId = r.surveyorId, timestamp = r.timestamp,
                        answers = ans, latitude = r.latitude, longitude = r.longitude,
                        imageBase64 = img, signature = r.signature
                    )
                }
                val batch = SyncBatch(pin, deviceId, sv.id, dtos)
                val result = withContext(Dispatchers.IO) { api.sync(batch) }
                db.responseDao().markSynced(pending.map { it.signature }, pin)
                refreshCounts()
                status = "Sync OK (PIN $pin): ${result.accepted} aceptadas, " +
                        "${result.duplicates} duplicadas, ${result.rejected} rechazadas"
                navigate(Screen.HOME)
            } catch (e: Exception) {
                status = "Error de sync: ${e.message}"
            }
            busy = false
        }
    }

    private suspend fun loadLocalSurveys() {
        val local = db.surveyDao().getAll()
        surveys.clear()
        surveys.addAll(local.mapNotNull { runCatching { gson.fromJson(it.json, Survey::class.java) }.getOrNull() })
    }

    private suspend fun refreshCounts() {
        totalCount = db.responseDao().count()
        pendingCount = db.responseDao().countPending()
    }
}
