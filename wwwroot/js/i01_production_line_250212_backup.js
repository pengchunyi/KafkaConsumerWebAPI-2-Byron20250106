//// 定义设备列表
//const devices = [
//    { id: "auto-loader", name: "自動送板機" },
//    { id: "glue-machine", name: "自動點膠機" },
//    { id: "plugin-1", name: "RTM自動插件機 1" },
//    { id: "plugin-2", name: "RTM 自動插件機2" },
//    { id: "auto-plugin", name: "自動插件機" },
//    { id: "bending-machine", name: "折腳/扭腳一體機" },
//    { id: "manual-1", name: "人工台" },
//    { id: "spray", name: "噴霧機" },
//    { id: "wave-solder", name: "波峰焊錫爐", source: "CFX.A00.SO20050832", content: "RealTimeStoveDataDisplay.html" },
//    { id: "auto-unloader", name: "自動存板機" },
//    { id: "inline-ict", name: "在線ICT" },
//    { id: "manual-2", name: "人工台" },
//    { id: "vr-machine-3", name: "RTM 自動調VR機3" },
//    { id: "glue-machine-4", name: "RTM自動點膠機4*" },
//    { id: "screw-machine-5", name: "RTM 自動鎖螺絲機5*" },
//    { id: "glue-machine-6", name: "RTM 自動點膠機6*" },
//    { id: "screw-machine-7", name: "RTM 自動鎖螺絲機7*" },
//    { id: "testing", name: "N合一自動功能測試機" },
//    { id: "labeling-8", name: "RTM 自動貼標機8*" },
//    { id: "packing", name: "打包台" }
//];

//// 从 localStorage 载入状态
//const loadDeviceStates = () => {
//    const savedStates = localStorage.getItem("deviceStates");
//    return savedStates ? JSON.parse(savedStates) : {};
//};

//// 保存状态到 localStorage
//const saveDeviceStates = (states) => {
//    localStorage.setItem("deviceStates", JSON.stringify(states));
//};

//const deviceStates = loadDeviceStates();

//// 动态生成设备卡片
//const container = document.getElementById("equipment-container");

//devices.forEach(device => {
//    const div = document.createElement("div");
//    div.classList.add("equipment");
//    div.id = device.id;

//    const savedState = deviceStates[device.id];
//    const statusText = savedState?.status || "離線";
//    const energyText = savedState?.energy || "-";
//    const runText = savedState?.run || "-";

//    div.innerHTML = `
//        <h3>${device.name}</h3>
//        <p>狀態: <span class="status ${statusText === "合閘" ? "online" : "offline"}">${statusText}</span></p>
//        <p>當天用電量: <span>${energyText}</span> kWh</p>
//        <p>運行狀態: <span>${runText}</span></p>
//    `;

//    div.addEventListener("click", () => {
//        if (device.content) {
//            showModal(device.content);
//        }
//    });

//    container.appendChild(div);
//});

//// SignalR Setup
//const connection = new signalR.HubConnectionBuilder()
//    .withUrl("/hub/messageHub")
//    .build();

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

//                // 更新设备状态
//                statusElement.textContent = "合閘";
//                statusElement.classList.remove("offline");
//                statusElement.classList.add("online");

//                const energyText = messageBody.EnergyUsed ? `${messageBody.EnergyUsed.toFixed(2)}` : "-";
//                energyElement.textContent = energyText;
//                runElement.textContent = "正常";

//                deviceStates[device.id] = { status: "合閘", energy: energyText, run: "正常" };
//                saveDeviceStates(deviceStates);
//            }
//        }
//    }
//});

//connection.start()
//    .then(() => console.log("SignalR connected"))
//    .catch(err => console.error("SignalR connection error:", err));

//// 模态框控制
//const modal = document.getElementById("modal");
//const closeModal = document.querySelector(".close");
//const iframe = document.getElementById("modal-iframe");

//function showModal(contentPath) {
//    iframe.src = contentPath;
//    modal.style.display = "flex";
//}

//closeModal.addEventListener("click", () => {
//    modal.style.display = "none";
//    iframe.src = "";
//});

//modal.addEventListener("click", (e) => {
//    if (e.target === modal) {
//        modal.style.display = "none";
//        iframe.src = "";
//    }
//});



// 定义设备列表
const devices = [
    { id: "auto-loader", name: "自動送板機" },
    { id: "glue-machine", name: "自動點膠機" },
    { id: "plugin-1", name: "RTM自動插件機1" },
    { id: "plugin-2", name: "RTM 自動插件機2" },
    { id: "auto-plugin", name: "自動插件機" },
    { id: "bending-machine", name: "折腳/扭腳一體機" },
    { id: "manual-1", name: "人工台" },
    { id: "spray", name: "噴霧機" },
    { id: "wave-solder", name: "波峰焊錫爐", source: "CFX.A00.SO20050832", content: "RealTimeStoveDataDisplay.html" },
    { id: "auto-unloader", name: "自動存板機" },
    { id: "inline-ict", name: "在線ICT" },
    { id: "manual-2", name: "人工台" },
    { id: "vr-machine-3", name: "RTM 自動調VR機3" },
    { id: "glue-machine-4", name: "RTM自動點膠機4*" },
    { id: "screw-machine-5", name: "RTM 自動鎖螺絲機5*" },
    { id: "glue-machine-6", name: "RTM 自動點膠機6*" },
    { id: "screw-machine-7", name: "RTM 自動鎖螺絲機7*" },
    { id: "testing", name: "N合一自動功能測試機" },
    { id: "labeling-8", name: "RTM 自動貼標機8*" },
    { id: "packing", name: "打包台" }
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
