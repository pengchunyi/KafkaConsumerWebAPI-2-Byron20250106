// 定义设备列表
const devices = [
    { id: "auto-loader", name: "自動送板機", source: "S217032102" },         // 自動送板機
    { id: "turning-machine", name: "轉板機", source: "S222100611" },         // 轉板機
    { id: "terminal-assembler", name: "端子自動組裝機", source: "S222239001" },  // 端子自動組裝機
    { id: "plugin-glue", name: "插件點膠機", source: "S222041501" },         // 插件點膠機
    { id: "rtm-plugin-2", name: "RTM插件機2", source: "S222063002" },        // RTM插件機2
    { id: "rtm-plugin", name: "RTM插件機", source: "S222090601" },           // RTM插件機
    { id: "ambu-plugin", name: "AMBU插件機", source: "S056421215", content: "RealTimeAMBUPluginDataDisplay.html" },  // AMBU插件機
    { id: "bending-machine", name: "扭腳機", source: "S223071802" },         // 扭腳機
    { id: "spray", name: "噴霧機", source: "SO20180432" },                  // 噴霧機
    { id: "wave-solder", name: "波峰焊錫爐", source: "SO20050832", content: "RealTimeStoveDataDisplay.html" },  // 波峰焊錫爐
    { id: "downhill-buffer", name: "下坡段緩存機", source: "S221111912" },    // 下坡段緩存機
    { id: "auto-strip-remover", name: "自動拆擋錫條", source: "S222349001" },  // 自動拆擋錫條
    { id: "double-aoi", name: "鐳晨雙面AOI", source: "S222741002" },         // 鐳晨雙面AOI
    { id: "ict-tester", name: "ICT自動測試機", source: "S021000701" },        // ICT自動測試機
    { id: "rtm-vr-tuner", name: "RTM自動調VR機", source: "ST06020001" },     // RTM自動調VR機
    { id: "rtm-glue-thermal", name: "RTM點膠涂散熱膏機", source: "ST00340083" },  // RTM點膠涂散熱膏機
    { id: "rtm-case-screw", name: "RTM鎖CASE螺絲機", source: "ST00420100" },  // RTM鎖CASE螺絲機
    { id: "rtm-glue", name: "RTM點膠機", source: "ST00340084" },             // RTM點膠機
    { id: "rtm-pcb-screw", name: "RTM鎖PCB螺絲機", source: "ST00420099" },    // RTM鎖PCB螺絲機
    { id: "three-in-one-tester", name: "3_IN1測試機", source: "S021107401" },  // 3_IN1測試機
    { id: "rtm-label", name: "RTM貼標機", source: "ST00440014" }             // RTM貼標機
];




// 从 localStorage 载入状态
const loadDeviceStates = () => {
    const savedStates = localStorage.getItem("deviceStates");
    return savedStates ? JSON.parse(savedStates) : {};
};

// 保存状态到 localStorage
const saveDeviceStates = (states) => {
    localStorage.setItem("deviceStates", JSON.stringify(states));
};

const deviceStates = loadDeviceStates();

// 动态生成设备卡片
const container = document.getElementById("equipment-container");

devices.forEach(device => {
    const div = document.createElement("div");
    div.classList.add("equipment");
    div.id = device.id;

    const savedState = deviceStates[device.id];
    const statusText = savedState?.status || "離線";
    const energyText = savedState?.energy || "-";
    const standbyEnergyText = savedState?.standbyEnergyText || "-";
    const runText = savedState?.run || "-";

    div.innerHTML = `
        <h3>${device.name}</h3>
        <p>狀態: <span class="status ${statusText === "合閘" ? "online" : "offline"}">${statusText}</span></p>
        <p>當天用電: <span>${energyText}</span> kWh</p>
        <p>待機用電: <span>${standbyEnergyText}</span> kWh</p>
        <p>運行狀態: <span>${runText}</span></p>
    `;

    div.addEventListener("click", () => {
        if (device.content) {
            showModal(device.content);
        }
    });

    container.appendChild(div);
});

// SignalR Setup
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub/messageHub")
    .build();



//250212_目前可以正常顯示卡片==================
connection.on("ReceiveMessage", (topic, message) => {
    const data = JSON.parse(message);
    const source = data.Data?.Data?.Meta?.Source;

    if (source) {
        const device = devices.find(d => source === d.source);
        if (device) {
            const messageBody = data.Data?.Data?.RawData?.MessageBody;
            if (messageBody) {
                const statusElement = document.getElementById(`${device.id}-status`);
                const energyElement = document.getElementById(`${device.id}-energy`);
                const runElement = document.getElementById(`${device.id}-run`);

                // 更新设备状态
                statusElement.textContent = "合閘";
                statusElement.classList.remove("offline");
                statusElement.classList.add("online");

                const energyText = messageBody.EnergyUsed ? `${messageBody.EnergyUsed.toFixed(2)}` : "-";
                energyElement.textContent = energyText;
                runElement.textContent = "正常";

                deviceStates[device.id] = { status: "合閘", energy: energyText, run: "正常" };

                saveDeviceStates(deviceStates);
            }
        }
    }
});
//250212_目前可以正常顯示卡片==================



//250212更新_新增運行狀態選項:異常以及顏色====================================
//connection.on("ReceiveMessage", (topic, message) => {
//    const data = JSON.parse(message);
//    const source = data.Data?.Data?.Meta?.Source;

//    if (source) {
//        const device = devices.find(d => source === d.source);
//        if (device) {
//            const messageBody = data.Data?.Data?.RawData?.MessageBody;
//            if (messageBody) {
//                const statusElement = document.getElementById(`${device.id}-status`);
//                const energyElement = document.getElementById(`${device.id}-energy`);
//                const runElement = document.getElementById(`${device.id}-run`);

//                // 先預設為正常運行
//                let runStatus = "正常";

//                // 判斷是否存在異常條件
//                if (/* 某些異常條件 */) {
//                    runStatus = "異常";
//                }

//                // 更新狀態
//                statusElement.textContent = "合閘";
//                statusElement.classList.remove("offline");
//                statusElement.classList.add("online");

//                const energyText = messageBody.EnergyUsed ? `${messageBody.EnergyUsed.toFixed(2)}` : "-";
//                energyElement.textContent = energyText;
//                // 根據運行狀態更新顏色
//                runElement.textContent = runStatus;


//                // 更新運行狀態顏色
//                if (runStatus === "正常") {
//                    runElement.classList.remove("abnormal");
//                    runElement.classList.add("normal");
//                } else if (runStatus === "異常") {
//                    runElement.classList.remove("normal");
//                    runElement.classList.add("abnormal");
//                }


//                deviceStates[device.id] = { status: "合閘", energy: energyText, run: runStatus };
//                saveDeviceStates(deviceStates);
//            }
//        }
//    }
//});
//250212更新_新增運行狀態選項:異常以及顏色====================================






connection.start()
    .then(() => console.log("SignalR connected"))
    .catch(err => console.error("SignalR connection error:", err));

// 模态框控制
const modal = document.getElementById("modal");
const closeModal = document.querySelector(".close");
const iframe = document.getElementById("modal-iframe");

function showModal(contentPath) {
    iframe.src = contentPath;
    // 在顯示模態框前更新表格中的數據
    updateDeviceCardStates();
    modal.style.display = "flex";
}

closeModal.addEventListener("click", () => {
    modal.style.display = "none";
    iframe.src = "";
});

modal.addEventListener("click", (e) => {
    if (e.target === modal) {
        modal.style.display = "none";
        iframe.src = "";
    }
});

// 顯示設備卡片的最新數據
function updateDeviceCardStates() {
    devices.forEach(device => {
        const savedState = deviceStates[device.id];
        const statusText = savedState?.status || "離線";
        const energyText = savedState?.energy || "-";
        const standbyEnergyText = savedState?.standbyEnergyText || "-";
        const runText = savedState?.run || "-";

        const div = document.getElementById(device.id);
        if (div) {
            div.innerHTML = `
                <h3>${device.name}</h3>
                <p>狀態: <span class="status ${statusText === "合閘" ? "online" : "offline"}">${statusText}</span></p>
                <p>當天用電: <span>${energyText}</span> kWh</p>
                <p>待機用電: <span>${standbyEnergyText}</span> kWh</p>
                <p>運行狀態: <span>${runText}</span></p>
            `;
        }
    });
}
