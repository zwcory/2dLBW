import torch
import torch.onnx
from train_lbw_model import LBWPredictor
import os

def export_to_onnx_legacy(
        model_path='lbw_model_best.pth',
        output_path='lbw_model_legacy.onnx'
):
    """Export using legacy ONNX exporter for guaranteed opset 15"""

    print("Exporting with legacy exporter (forces opset 15)...")

    model = LBWPredictor(input_size=13)
    model.load_state_dict(torch.load(model_path))
    model.eval()

    dummy_input = torch.randn(1, 13)

    # Use legacy exporter (pre-PyTorch 2.1)
    with torch.no_grad():
        torch.onnx.export(
            model,
            dummy_input,
            output_path,
            export_params=True,
            opset_version=13,  # Use 13 for better compatibility
            do_constant_folding=True,
            input_names=['input'],
            output_names=['output'],
            dynamic_axes={
                'input': {0: 'batch_size'},
                'output': {0: 'batch_size'}
            },
            # Force legacy exporter
            dynamo=False
        )

    print(f"✓ Exported to {output_path}")

    # Verify
    try:
        import onnx
        model = onnx.load(output_path)
        print(f"✓ Opset version: {model.opset_import[0].version}")
    except:
        pass

if __name__ == '__main__':
    export_to_onnx_legacy()