import onnx

model = onnx.load('lbw_model_legacy.onnx')
print(f"Actual opset version: {model.opset_import[0].version}")