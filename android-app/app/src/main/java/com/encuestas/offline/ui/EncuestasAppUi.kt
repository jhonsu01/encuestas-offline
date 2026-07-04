package com.encuestas.offline.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Checkbox
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.encuestas.offline.domain.DocumentType
import com.encuestas.offline.domain.Question
import com.encuestas.offline.domain.Survey

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EncuestasAppUi(vm: MainViewModel = viewModel()) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("📋 Encuestas Offline") },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        bottomBar = {
            Row(
                Modifier.fillMaxWidth().padding(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (vm.busy) {
                    CircularProgressIndicator(Modifier.width(20.dp))
                    Text("  ")
                }
                Text(vm.status, style = MaterialTheme.typography.bodySmall)
            }
        }
    ) { padding ->
        Box(Modifier.padding(padding).fillMaxSize()) {
            when (vm.screen) {
                Screen.REGISTRO -> RegistroScreen(vm)
                Screen.HOME -> HomeScreen(vm)
                Screen.FORM -> FormScreen(vm)
                Screen.CAMARA -> CameraCapture(
                    onCaptured = vm::onImageCaptured,
                    onCancel = { vm.navigate(Screen.FORM) }
                )
                Screen.CIERRE -> CierreScreen(vm)
            }
        }
    }
}

@Composable
private fun RegistroScreen(vm: MainViewModel) {
    var type by remember { mutableStateOf(DocumentType.CC) }
    var doc by remember { mutableStateOf("") }
    var name by remember { mutableStateOf("") }
    var pin by remember { mutableStateOf("") }

    Column(Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Registro de encuestador", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleLarge)
        Text("Tipo de documento")
        Row(verticalAlignment = Alignment.CenterVertically) {
            DocumentType.values().forEach { t ->
                Row(
                    Modifier.selectable(selected = type == t, onClick = { type = t }).padding(end = 16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    RadioButton(selected = type == t, onClick = { type = t })
                    Text(if (t == DocumentType.CC) "Cédula (CC)" else "Extranjería (CE)")
                }
            }
        }
        OutlinedTextField(
            value = doc, onValueChange = { doc = it }, label = { Text("Número de documento") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            value = name, onValueChange = { name = it }, label = { Text("Nombre completo") },
            modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            value = pin, onValueChange = { pin = it.filter { c -> c.isDigit() } }, label = { Text("PIN local (4-8 dígitos)") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword), modifier = Modifier.fillMaxWidth()
        )
        Button(onClick = { vm.registrar(type, doc, name, pin) }, modifier = Modifier.fillMaxWidth()) {
            Text("Registrarme")
        }
    }
}

@Composable
private fun HomeScreen(vm: MainViewModel) {
    var host by remember { mutableStateOf("") }

    LazyColumn(Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    Text("Encuestador", fontWeight = FontWeight.Bold)
                    Text(vm.surveyor?.let { "${it.fullName} (${it.id})" } ?: "—")
                    Text("Capturadas: ${vm.totalCount}  |  Pendientes de sync: ${vm.pendingCount}")
                }
            }
        }
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Servidor central", fontWeight = FontWeight.Bold)
                    Text(vm.server?.let { "${it.host}:${it.port} (${it.method})" } ?: "No detectado")
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(onClick = { vm.buscarServidor() }) { Text("🔎 Autodetectar") }
                        Button(onClick = { vm.descargarEncuestas() }) { Text("⬇ Encuestas") }
                    }
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedTextField(
                            value = host, onValueChange = { host = it }, label = { Text("IP manual") },
                            modifier = Modifier.weight(1f)
                        )
                        OutlinedButton(onClick = { vm.setServidorManual(host, 5000) }) { Text("Usar") }
                    }
                }
            }
        }
        item {
            Text("Encuestas disponibles", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleMedium)
        }
        if (vm.surveys.isEmpty()) {
            item { Text("Aún no hay encuestas. Autodetecta el servidor y descárgalas.") }
        } else {
            items(vm.surveys) { s ->
                Card(Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(12.dp)) {
                        Text(s.title, fontWeight = FontWeight.Bold)
                        s.description?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                        Text("${s.questions.size} preguntas • v${s.version}", style = MaterialTheme.typography.bodySmall)
                        Button(onClick = { vm.abrirEncuesta(s) }, modifier = Modifier.padding(top = 8.dp)) {
                            Text("Diligenciar")
                        }
                    }
                }
            }
        }
        item {
            HorizontalDivider()
            Button(
                onClick = { vm.navigate(Screen.CIERRE) },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                enabled = vm.pendingCount > 0
            ) { Text("🔒 Cierre diario / Sincronizar (${vm.pendingCount})") }
        }
    }
}

@Composable
private fun FormScreen(vm: MainViewModel) {
    val survey = vm.currentSurvey ?: return
    LazyColumn(Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { Text(survey.title, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleLarge) }
        items(survey.questions) { q -> QuestionInput(vm, q) }
        item {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = { vm.navigate(Screen.HOME) }, modifier = Modifier.weight(1f)) { Text("Volver") }
                Button(onClick = { vm.guardarRespuesta() }, modifier = Modifier.weight(1f)) { Text("Guardar") }
            }
        }
    }
}

@Composable
private fun QuestionInput(vm: MainViewModel, q: Question) {
    val value = vm.answers[q.id] ?: ""
    Column(Modifier.fillMaxWidth()) {
        Text(buildString { append(q.label); if (q.required) append(" *") }, fontWeight = FontWeight.SemiBold)
        when (q.type) {
            "single_choice" -> q.options.orEmpty().forEach { opt ->
                Row(
                    Modifier.fillMaxWidth().selectable(selected = value == opt, onClick = { vm.setAnswer(q.id, opt) }),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    RadioButton(selected = value == opt, onClick = { vm.setAnswer(q.id, opt) })
                    Text(opt)
                }
            }
            "multiple_choice" -> {
                val selected = value.split(",").filter { it.isNotBlank() }.toSet()
                q.options.orEmpty().forEach { opt ->
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Checkbox(checked = selected.contains(opt), onCheckedChange = { vm.toggleMultiple(q.id, opt) })
                        Text(opt)
                    }
                }
            }
            "number" -> OutlinedTextField(
                value = value, onValueChange = { vm.setAnswer(q.id, it.filter { c -> c.isDigit() }) },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), modifier = Modifier.fillMaxWidth()
            )
            "date" -> OutlinedTextField(
                value = value, onValueChange = { vm.setAnswer(q.id, it) },
                label = { Text("YYYY-MM-DD") }, modifier = Modifier.fillMaxWidth()
            )
            "image" -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { vm.navigate(Screen.CAMARA) }) { Text("📷 Tomar foto") }
                Text(if (vm.imagePath != null) "✓ Imagen lista" else "Sin imagen")
            }
            "gps" -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { vm.capturarGps() }) { Text("📍 Capturar GPS") }
                Text(if (vm.lat != null) "✓ %.4f, %.4f".format(vm.lat, vm.lon) else "Sin ubicación")
            }
            else -> OutlinedTextField(
                value = value, onValueChange = { vm.setAnswer(q.id, it) }, modifier = Modifier.fillMaxWidth()
            )
        }
    }
}

@Composable
private fun CierreScreen(vm: MainViewModel) {
    var pin by remember { mutableStateOf("") }
    Column(Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Cierre diario", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleLarge)
        Text("Confirma tu identidad con el PIN local para firmar el cierre y generar el PIN de sincronización del lote.")
        Text("Respuestas pendientes: ${vm.pendingCount}")
        vm.syncPin?.let { Text("PIN de sincronización del lote: $it", fontWeight = FontWeight.Bold) }
        OutlinedTextField(
            value = pin, onValueChange = { pin = it.filter { c -> c.isDigit() } },
            label = { Text("PIN local") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
            modifier = Modifier.fillMaxWidth()
        )
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = { vm.navigate(Screen.HOME) }, modifier = Modifier.weight(1f)) { Text("Volver") }
            Button(onClick = { vm.cerrarDiaYSincronizar(pin) }, modifier = Modifier.weight(1f)) {
                Text("Firmar y sincronizar")
            }
        }
    }
}
