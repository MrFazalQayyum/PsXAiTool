"""Run once to generate VAPID keys for Web Push."""
import base64
from py_vapid import Vapid
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

v = Vapid()
v.generate_keys()

priv_pem = v.private_pem().decode().strip()
pub_bytes = v.public_key.public_bytes(Encoding.X962, PublicFormat.UncompressedPoint)
pub_b64 = base64.urlsafe_b64encode(pub_bytes).decode().rstrip("=")

print("Add these to backend/.env:\n")
print(f'VAPID_PRIVATE_KEY="{priv_pem}"')
print(f"VAPID_PUBLIC_KEY={pub_b64}")
print(f'VAPID_CLAIMS_EMAIL=mailto:mr.fazalqayyum@gmail.com')
