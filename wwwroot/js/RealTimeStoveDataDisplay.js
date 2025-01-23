// 更新當前時間
function updateTime() {
    const currentTime = new Date();
    document.getElementById("current-time").innerText = currentTime.toLocaleString();
}
setInterval(updateTime, 1000);
updateTime();

// 初始化 rowData
const rowData = {
    power: { name: "總電源", currentNowRYB: ["-", "-", "-"], powerNowRYB: ["-", "-", "-"], energyUsed: "-", mainTemp: ["-", "-", "-"], switchStatus: { s1: "-" } },
    preheat: { name: "預熱", currentNowRYB: ["-", "-", "-"], powerNowRYB: ["-", "-", "-"], energyUsed: "-", preheatTemp: ["-", "-", "-"], switchStatus: { s2: "-" } },
    trough: { name: "錫槽", currentNowRYB: ["-", "-", "-"], powerNowRYB: ["-", "-", "-"], energyUsed: "-", tinBathTemp: ["-", "-", "-"], switchStatus: { s3: "-" } },
};

// 從 localStorage 加載數據
const savedData = JSON.parse(localStorage.getItem("lastData"));
if (savedData) {
    Object.keys(savedData).forEach(key => {
        if (rowData[key]) rowData[key] = savedData[key];
    });
} else {
    //250123新增=============================================================
    localStorage.setItem("lastData", JSON.stringify(rowData));
    //250123新增=============================================================
}

//250123新增=============================================================
// 頁面加載後立即渲染表格
updateTable();
console.log("Initial table render:", rowData);
//250123新增=============================================================

// 建立 SignalR 連線
const connection = new signalR.HubConnectionBuilder().withUrl("/hub/messageHub").build();

connection.on("ReceiveMessage", (topic, message) => {
    const parsed = JSON.parse(message);

    if (topic === "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed") {
        const source = parsed.Data.Data.Meta.Source;
        const key = mapSourceToKey(source);
        if (!key) return;

        const mb = parsed.Data.Data.RawData.MessageBody;
        rowData[key].currentNowRYB = mb.CurrentNowRYB.map(x => x ?? "-");
        rowData[key].powerNowRYB = mb.PowerNowRYB.map(x => x ?? "-");
        rowData[key].energyUsed = mb.EnergyUsed ?? "-";
    } else if (topic === "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified") {
        const modifiedParams = parsed.Data.Data.RawData.MessageBody.ModifiedParameters;
        modifiedParams.forEach(param => {
            const name = param.Name;
            const val = param.Value;
            if (name.startsWith("OV_MainTemperature")) updateTemperature(rowData.power.mainTemp, name, val);
            else if (name.startsWith("OV_PreheatTemperature")) updateTemperature(rowData.preheat.preheatTemp, name, val);
            else if (name.startsWith("OV_TinBathTemperature")) updateTemperature(rowData.trough.tinBathTemp, name, val);
            else updateSwitchStatus(name, val);
        });
    }

    localStorage.setItem("lastData", JSON.stringify(rowData));
    console.log("Updated from Kafka and saved to localStorage:", rowData);
    updateTable();
});

function updateTemperature(tempArray, name, value) {
    const phase = name.slice(-1);
    const idx = phase === "A" ? 0 : phase === "B" ? 1 : 2;
    tempArray[idx] = value;
}

function updateSwitchStatus(name, value) {
    if (name === "MSP_SwitchStatus1") rowData.power.switchStatus.s1 = value;
    else if (name === "MSP_SwitchStatus2") rowData.preheat.switchStatus.s2 = value;
    else if (name === "MSP_SwitchStatus3") rowData.trough.switchStatus.s3 = value;
}

// 渲染表格
function updateTable() {
    const tableBody = document.getElementById("data-rows");
    let html = "";

    ["power", "preheat", "trough"].forEach(key => {
        const row = rowData[key];
        const [cA, cB, cC] = row.currentNowRYB;
        const [pA, pB, pC] = row.powerNowRYB;
        const engUsed = row.energyUsed;

        const [tA, tB, tC] = key === "power" ? row.mainTemp : key === "preheat" ? row.preheatTemp : row.tinBathTemp;
        const switchDisplay = key === "power" ? getSwitchDisplay(row.switchStatus.s1) : key === "preheat" ? getSwitchDisplay(row.switchStatus.s2) : getSwitchDisplay(row.switchStatus.s3);
        const runStatus = "正常";
        const tempSetting = "100";

        html += `
            <tr>
                <td>${row.name}</td>
                <td>${cA}</td><td>${cB}</td><td>${cC}</td>
                <td>${pA}</td><td>${pB}</td><td>${pC}</td>
                <td>${tA}</td><td>${tB}</td><td>${tC}</td>
                <td>${engUsed}</td>
                <td class="status normal">${switchDisplay}</td>
                <td class="status normal">${runStatus}</td>
                <td class="status normal">${tempSetting}</td>
            </tr>
        `;
    });

    tableBody.innerHTML = html;


    //250123新增=============================================================
    // 保存更新到 localStorage
    localStorage.setItem("lastData", JSON.stringify(rowData));
    console.log("Table updated and saved to localStorage:", rowData);
    //250123新增=============================================================
}

function getSwitchDisplay(status) {
    return status === "0" ? "合閘" : status === "-" ? "-" : "分閘";
}

connection.start().then(() => console.log("SignalR connected")).catch(err => console.error("SignalR error:", err));
